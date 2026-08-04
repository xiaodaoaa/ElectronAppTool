using System.Drawing;
using System.Windows;
using WF = System.Windows.Forms;

namespace NtpTool.App.Services;

/// <summary>
/// 系统托盘图标。提供主窗口显隐、快捷同步/启停服务端、退出等托盘菜单与双击交互。
/// 关闭主窗口时最小化到托盘，托盘"退出"才会真正结束进程。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _notifyIcon;
    private readonly Action _showWindow;
    private readonly Action _exitApp;
    private Action? _toggleServer;
    private Func<bool>? _isServerRunning;
    private Func<string, string, bool>? _confirm;

    public event EventHandler? SyncRequested;
    public event EventHandler? SettingsRequested;

    public TrayIcon(Icon icon, Action showWindow, Action exitApp)
    {
        _showWindow = showWindow;
        _exitApp = exitApp;

        _notifyIcon = new WF.NotifyIcon
        {
            Icon = icon,
            Text = "NTP TimeSync Tool",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => _showWindow();
    }

    public void BindServerActions(Action toggleServer, Func<bool> isServerRunning, Func<string, string, bool> confirm)
    {
        _toggleServer = toggleServer;
        _isServerRunning = isServerRunning;
        _confirm = confirm;
        _notifyIcon.ContextMenuStrip = BuildMenu(); // 重建以反映可支配项
    }

    public void ShowBalloon(string title, string message, WF.ToolTipIcon icon = WF.ToolTipIcon.Info, int timeoutMs = 2000)
    {
        _notifyIcon.ShowBalloonTip(timeoutMs, title, message, icon);
    }

    private WF.ContextMenuStrip BuildMenu()
    {
        var menu = new WF.ContextMenuStrip();

        var show = new WF.ToolStripMenuItem("显示主界面", null, (_, _) => _showWindow());

        var sync = new WF.ToolStripMenuItem("立即同步", null, (_, _) => SyncRequested?.Invoke(this, EventArgs.Empty));

        WF.ToolStripMenuItem? server = null;
        if (_toggleServer is not null && _isServerRunning is not null)
        {
            string label = _isServerRunning() ? "停止服务端" : "启动服务端";
            server = new WF.ToolStripMenuItem(label, null, (_, _) =>
            {
                if (_isServerRunning() && _confirm is not null)
                {
                    if (!_confirm("停止服务端", "确定要停止 NTP 服务端吗？"))
                    {
                        return;
                    }
                }

                _toggleServer();
            });
        }

        var exit = new WF.ToolStripMenuItem("退出", null, (_, _) => _exitApp());
        var settings = new WF.ToolStripMenuItem("设置", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));

        menu.Items.Add(show);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(sync);
        if (server is not null)
        {
            menu.Items.Add(server);
        }

        menu.Items.Add(settings);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(exit);

        return menu;
    }

    /// <summary>刷新托盘菜单中服务端项的标签（状态变化后调用）。</summary>
    public void RefreshServerMenuItem()
    {
        _notifyIcon.ContextMenuStrip = BuildMenu();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}