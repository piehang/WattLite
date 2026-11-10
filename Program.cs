using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WattLite;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromLibs;

        if (args.Length > 0 && args[0] == "--cleanup")
        {
            try { HostsFile.Remove(); Log.Write("已清理 hosts 中的 WattLite 区块"); }
            catch (Exception ex) { Log.Write("清理 hosts 失败: " + ex.Message); }
            return;
        }

        if (args.Length > 0 && args[0] == "--selftest")
        {
            var holdSeconds = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 0;
            RunSelfTestAsync(holdSeconds).GetAwaiter().GetResult();
            return;
        }

        using var mutex = new Mutex(true, @"Global\WattLite.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WattLite 已在运行（请查看系统托盘）。", "WattLite", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Log.Write("==== WattLite 启动 ====");
        try { HostsFile.Remove(); } catch (Exception ex) { Log.Write("启动时清理 hosts 失败: " + ex.Message); }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());
        Log.Write("==== WattLite 退出 ====");
    }

    static async Task RunSelfTestAsync(int holdSeconds)
    {
        Log.Write("==== 自检模式开始 ====");
        var session = new AcceleratorSession();
        try
        {
            await session.StartAsync();
            Log.Write("SELFTEST: 启动成功");
            if (holdSeconds > 0)
            {
                Log.Write($"SELFTEST: 保持 {holdSeconds}s");
                await Task.Delay(TimeSpan.FromSeconds(holdSeconds));
            }
        }
        catch (Exception ex)
        {
            Log.Write("SELFTEST: 启动失败 " + ex);
        }

        try
        {
            await session.StopAsync();
            Log.Write("SELFTEST: 停止完成");
        }
        catch (Exception ex)
        {
            Log.Write("SELFTEST: 停止失败 " + ex);
        }
        Log.Write("==== 自检模式结束 ====");
    }

    static Assembly ResolveFromLibs(object sender, ResolveEventArgs args)
    {
        try
        {
            var name = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(name)) return null;
            var path = Path.Combine(AppContext.BaseDirectory, "libs", name + ".dll");
            if (File.Exists(path))
            {
                Log.Write("AssemblyResolve: " + name);
                return Assembly.LoadFrom(path);
            }
        }
        catch (Exception ex)
        {
            Log.Write("AssemblyResolve 失败: " + args.Name + " -> " + ex.Message);
        }
        return null;
    }
}
