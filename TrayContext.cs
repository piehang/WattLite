using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WattLite;

internal sealed class TrayContext : ApplicationContext
{
    static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon != null) return icon;
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    readonly NotifyIcon _icon;
    readonly ContextMenuStrip _menu;
    readonly ToolStripMenuItem _startItem;
    readonly ToolStripMenuItem _stopItem;
    readonly AcceleratorSession _session = new();
    readonly Timer _autoStartTimer;
    bool _busy;
    bool _exiting;
    Task _running = Task.CompletedTask;

    public TrayContext()
    {
        _startItem = new ToolStripMenuItem("启动加速", null, async (_, _) => await RunAsync(StartAsync));
        _stopItem = new ToolStripMenuItem("停止加速", null, async (_, _) => await RunAsync(StopAsync)) { Enabled = false };
        var openLog = new ToolStripMenuItem("查看日志", null, (_, _) => OpenLog());
        var openDir = new ToolStripMenuItem("打开程序目录", null, (_, _) => Process.Start("explorer.exe", AppContext.BaseDirectory));
        var exit = new ToolStripMenuItem("退出", null, (_, _) => BeginExit());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_startItem);
        _menu.Items.Add(_stopItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(openLog);
        _menu.Items.Add(openDir);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exit);

        _icon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "WattLite - 启动中…",
            ContextMenuStrip = _menu,
        };
        _icon.DoubleClick += async (_, _) => await RunAsync(_session.IsRunning ? StopAsync : StartAsync);
        _icon.ShowBalloonTip(3000, "WattLite", "已启动，正在自动开启加速…", ToolTipIcon.Info);

        // 消息循环就绪后自动开启加速
        _autoStartTimer = new Timer { Interval = 300 };
        _autoStartTimer.Tick += async (_, _) =>
        {
            _autoStartTimer.Stop();
            await RunAsync(StartAsync);
        };
        _autoStartTimer.Start();
    }

    Task RunAsync(Func<Task> action)
    {
        if (_busy || _exiting) return Task.CompletedTask;
        _running = GuardAsync(action);
        return _running;
    }

    /// <summary>
    /// 退出入口。不能直接在 ToolStripDropDown 的点击处理里调用 ExitThread()：
    /// 此时右键菜单自身还在处理消息，主消息循环的退出请求会被吞掉，表现为点退出后卡死。
    /// 所以先关闭菜单，再把真正的退出流程排到消息队列后面执行。
    /// </summary>
    void BeginExit()
    {
        try { _menu.Close(); } catch { }
        var ctx = System.Threading.SynchronizationContext.Current;
        if (ctx != null) ctx.Post(_ => _ = ExitAsync(), null);
        else _ = ExitAsync();
    }

    async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;

        SetMenuEnabled(false);
        try { _icon.Text = "WattLite - 正在退出…"; } catch { }

        // 退出前先还原 hosts 并结束加速器子进程，避免残留域名映射和孤儿进程
        try
        {
            if (_busy)
                await Task.WhenAny(_running, Task.Delay(TimeSpan.FromSeconds(3)));
            await _session.StopAsync().WaitAsync(TimeSpan.FromSeconds(12));
            Log.Write("退出前已停止加速");
        }
        catch (Exception ex)
        {
            Log.Write("退出时停止加速异常: " + ex.Message);
        }

        // 先摘掉托盘图标，避免进程结束后残留“幽灵图标”（点它没反应，看着像卡死）
        try { _icon.Visible = false; } catch { }

        // 兜底：万一消息循环被模态窗口等卡住，15 秒后强制结束进程
        // （正常路径 10 秒内完成，留出余量保证 hosts 一定被还原）
        _ = Task.Run(async () => { await Task.Delay(TimeSpan.FromSeconds(15)); Environment.Exit(0); });
        ExitThread();
    }

    async Task GuardAsync(Func<Task> action)
    {
        _busy = true;
        SetMenuEnabled(false);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log.Write("操作失败: " + ex);
            _icon.ShowBalloonTip(5000, "WattLite 出错", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            _busy = false;
            if (!_exiting)
            {
                SetMenuEnabled(true);
                RefreshState();
            }
        }
    }

    async Task StartAsync()
    {
        _icon.Text = "WattLite - 启动中…";
        await _session.StartAsync();
        _icon.ShowBalloonTip(3000, "WattLite", "加速已开启（Steam / Twitch 等）。", ToolTipIcon.Info);
    }

    async Task StopAsync()
    {
        _icon.Text = "WattLite - 停止中…";
        await _session.StopAsync();
        _icon.ShowBalloonTip(3000, "WattLite", "加速已停止，hosts 已还原。", ToolTipIcon.Info);
    }

    void RefreshState()
    {
        var running = _session.IsRunning;
        _startItem.Enabled = !running;
        _stopItem.Enabled = running;
        _icon.Text = running ? "WattLite - 加速中（Steam/Twitch）" : "WattLite - 未加速（Steam/Twitch）";
    }

    void SetMenuEnabled(bool enabled)
    {
        _startItem.Enabled = enabled && !_session.IsRunning;
        _stopItem.Enabled = enabled && _session.IsRunning;
    }

    static void OpenLog()
    {
        try { Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("打开日志失败: " + ex.Message); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _autoStartTimer.Stop(); _autoStartTimer.Dispose(); } catch { }
            try { _menu.Close(); } catch { }

            // 不能在 UI 线程上同步等 async 方法的续体（续体要回 UI 线程，会自锁）；
            // 丢到线程池执行，并给一个上限，避免退出时无限等待。
            try
            {
                if (!Task.Run(() => _session.StopAsync()).Wait(TimeSpan.FromSeconds(12)))
                    Log.Write("退出清理超时，已放弃等待");
            }
            catch (Exception ex) { Log.Write("退出清理异常: " + ex.Message); }

            try { _icon.Visible = false; } catch { }
            _icon.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
