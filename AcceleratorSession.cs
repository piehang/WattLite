using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BD.WTTS.Enums;
using BD.WTTS.Models;
using BD.WTTS.Services;
using dotnetCampus.Ipc.CompilerServices.GeneratedProxies;
using dotnetCampus.Ipc.Context;
using dotnetCampus.Ipc.Pipes;

namespace WattLite;

/// <summary>
/// 拉起官方加速器子进程，并通过 IPC 通知其开始/停止反向代理。
/// 架构：父进程（本程序）建管道 ipc_xxx 作为服务端 → 以 3 个参数启动 Steam++.Accelerator.exe
/// → 子进程建管道 ipc_xxx_Accelerator 并反连父进程 → 父进程 GetAndConnectToPeerAsync 连上子进程
/// → CreateIpcProxy&lt;IReverseProxyService&gt;() → StartProxyAsync(settings)。
/// </summary>
internal sealed class AcceleratorSession
{
    const string ModuleName = "Accelerator";

    Process _child;
    IpcProvider _ipc;
    IReverseProxyService _proxy;
    string _pipeName;
    IReadOnlyList<AccelerateProjectDTO> _projects;

    public bool IsRunning { get; private set; }

    public static string AcceleratorExePath => Path.Combine(AppContext.BaseDirectory, "Accelerator", "Steam++.Accelerator.exe");

    static string StoreLocalState => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "4651ED44255E.47979655102CE_k6txddmbb6c52", "LocalState");

    public async Task StartAsync()
    {
        if (IsRunning) return;
        if (!File.Exists(AcceleratorExePath))
            throw new FileNotFoundException("未找到加速器子进程", AcceleratorExePath);

        _projects = DomainCatalog.Load();
        if (_projects.Count == 0)
            throw new InvalidOperationException("加速项目为空");

        var appData = StoreLocalState;
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "4651ED44255E.47979655102CE_k6txddmbb6c52", "LocalCache");

        _pipeName = "ipc_" + Guid.NewGuid().ToString("N");
        Log.Write($"创建 IPC 管道服务端: {_pipeName}");

        var config = new IpcConfiguration();
        _ipc = new IpcProvider(_pipeName, config);
        _ipc.StartServer();

        var argument = SubProcessArgs.Create(appData, cacheDir);
        var dotnetRoot = DotNetRuntime.Locate()
            ?? throw new DirectoryNotFoundException("未找到 .NET 10 运行时，请安装 Watt Toolkit，或将 dotnet 运行时目录放到本程序目录下的 dotnet 文件夹");
        Log.Write("子进程 DOTNET_ROOT = " + dotnetRoot);

        var psi = new ProcessStartInfo(AcceleratorExePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(AcceleratorExePath),
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["DOTNET_ROOT"] = dotnetRoot;
        psi.Environment["DOTNET_ROOT_X64"] = dotnetRoot;
        psi.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        psi.ArgumentList.Add(_pipeName);
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        psi.ArgumentList.Add(argument);

        _child = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _child.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log.Write("[acc:out] " + e.Data); };
        _child.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log.Write("[acc:err] " + e.Data); };
        _child.Exited += (_, _) => Log.Write($"加速器进程退出，ExitCode={(TryGetExitCode())}");
        _child.Start();
        _child.BeginOutputReadLine();
        _child.BeginErrorReadLine();
        Log.Write($"已启动加速器子进程 PID={_child.Id}");

        var peerName = _pipeName + "_" + ModuleName;
        var connectTask = _ipc.GetAndConnectToPeerAsync(peerName);
        var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(30)));
        if (completed != connectTask)
            throw new TimeoutException("连接加速器子进程 IPC 超时（30s）");
        var peer = await connectTask;
        Log.Write("已连接加速器 IPC 对端: " + peerName);

        _proxy = _ipc.CreateIpcProxy<IReverseProxyService>(peer);

        var settings = BuildSettings(_projects);
        var payload = MemoryPack.MemoryPackSerializer.Serialize(settings);
        Log.Write($"ReverseProxySettings 序列化 {payload.Length} 字节");

        var result = await _proxy.StartProxyAsync(payload);
        Log.Write("StartProxyAsync 返回: " + result.Code);
        if (result.Code != StartProxyResultCode.Ok)
            throw new InvalidOperationException($"启动反向代理失败: {result.Code} {result.Exception?.Message}");

        var domains = DomainCatalog.CollectHostsDomains(_projects);
        HostsFile.Apply(domains);
        Log.Write($"hosts 已写入 {domains.Count} 个域名");

        IsRunning = true;
    }

    public async Task StopAsync()
    {
        if (!IsRunning && _child == null) return;

        try
        {
            if (_proxy != null)
            {
                // 子进程可能已异常退出，IPC 调用会一直挂着，这里限时 8 秒
                var stopTask = Task.Run(async () => await _proxy.StopProxyAsync());
                if (await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(8))) != stopTask)
                {
                    Log.Write("StopProxyAsync 超时（子进程可能已失联），强制结束");
                }
                else
                {
                    await stopTask;
                    Log.Write("StopProxyAsync 完成");
                }
            }
        }
        catch (Exception ex) { Log.Write("StopProxyAsync 异常: " + ex.Message); }

        try { HostsFile.Remove(); Log.Write("hosts 已清理"); }
        catch (Exception ex) { Log.Write("清理 hosts 失败: " + ex.Message); }

        try
        {
            if (_child is { HasExited: false }) _child.Kill(entireProcessTree: true);
        }
        catch (Exception ex) { Log.Write("结束子进程失败: " + ex.Message); }

        _proxy = null;
        _child = null;
        IsRunning = false;
    }

    int TryGetExitCode()
    {
        try { return _child?.ExitCode ?? -1; } catch { return -1; }
    }

    static ReverseProxySettings BuildSettings(IReadOnlyList<AccelerateProjectDTO> projects) => new(
        projects,
        null,
        false,
        false,
        (ushort)26561,
        "0.0.0.0",
        ProxyMode.Hosts,
        false,
        false,
        true,
        false,
        (ushort)0,
        false,
        ExternalProxyType.Socks5,
        null,
        (ushort)0,
        null,
        null,
        "223.5.5.5",
        false,
        false,
        null,
        null);
}
