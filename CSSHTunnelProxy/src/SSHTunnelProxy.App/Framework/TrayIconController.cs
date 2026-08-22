using Hardcodet.Wpf.TaskbarNotification;
using SSHTunnelProxy.App.ViewModels;
using SSHTunnelProxy.App.Views;
using SSHTunnelProxy.Core.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SSHTunnelProxy.App.Framework;

/// <summary>
/// 系统托盘控制器：维护托盘图标与快捷菜单，并根据隧道状态更新工具提示。
/// 图标统一使用应用资源 Assets/app.ico。
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;
    private readonly MainViewModel _main;
    private readonly ITunnelManager _manager;
    private readonly DispatcherUI _ui;

    public TrayIconController(
        MainViewModel main,
        ITunnelManager manager,
        Func<Window> mainWindowFactory)
    {
        _main = main;
        _manager = manager;
        _ui = new DispatcherUI();

        var menu = BuildContextMenu(mainWindowFactory);
        // 菜单打开时重建隧道项：新建/删除/编辑隧道后菜单才反映最新列表。
        // （菜单仅在构造时构建一次，否则会停留在启动时的旧隧道列表。）
        menu.Opened += (_, _) => RebuildTunnelItems(menu, mainWindowFactory);
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "SSHTunnelProxy",
            ContextMenu = menu,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
        };

        // 双击托盘图标：恢复并前置主窗口。
        _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow(mainWindowFactory);

        SubscribeState();
    }

    /// <summary>恢复并前置主窗口（供托盘双击与菜单共用）。</summary>
    private static void ShowMainWindow(Func<Window> mainWindowFactory)
    {
        var window = mainWindowFactory();
        if (window is MainWindow main)
            main.ShowFromTray();
        else
            window.Show();
    }

    private ContextMenu BuildContextMenu(Func<Window> mainWindowFactory)
    {
        var menu = new ContextMenu();

        var show = new System.Windows.Controls.MenuItem { Header = "显示主窗口" };
        show.Click += (_, _) => ShowMainWindow(mainWindowFactory);
        menu.Items.Add(show);

        // 隧道项占位：实际项在菜单打开时由 RebuildTunnelItems 填充。
        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
        exit.Click += (_, _) =>
        {
            // 放行关闭拦截，确保即使勾选"关闭时最小化到托盘"也能真正退出。
            MainWindow.IsQuitting = true;
            Application.Current.Shutdown();
        };
        menu.Items.Add(exit);

        return menu;
    }

    /// <summary>
    /// 重建菜单中的隧道项：在"显示主窗口"与分隔符之间插入当前所有隧道，
    /// 反映新建/删除/编辑后的最新列表。每次菜单打开时调用。
    /// </summary>
    private void RebuildTunnelItems(ContextMenu menu, Func<Window> mainWindowFactory)
    {
        // 先移除旧的隧道项（位于"显示主窗口"之后、分隔符之前的所有项）。
        // 菜单结构固定为：[显示主窗口, (隧道项...), Separator, 退出]。
        while (menu.Items.Count > 1 && menu.Items[1] is not System.Windows.Controls.Separator)
            menu.Items.RemoveAt(1);

        var insertAt = 1;
        foreach (var tunnel in _main.Tunnels)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                // 显示隧道名 + 当前连接状态（菜单打开瞬间的快照）。
                Header = $"{tunnel.Name}({StateText(tunnel.State)})",
            };
            item.Click += async (_, _) =>
            {
                if (tunnel.State == Core.Models.TunnelState.Connected ||
                    tunnel.State == Core.Models.TunnelState.Connecting)
                    await tunnel.StopCommand.ExecuteAsync(null);
                else
                    await tunnel.StartCommand.ExecuteAsync(null);
            };
            menu.Items.Insert(insertAt, item);
            insertAt++;
        }
    }

    /// <summary>把隧道状态枚举转成菜单中显示的中文文字。</summary>
    private static string StateText(Core.Models.TunnelState state) => state switch
    {
        Core.Models.TunnelState.Connected => "已连接",
        Core.Models.TunnelState.Connecting => "连接中",
        Core.Models.TunnelState.Reconnecting => "重连中",
        Core.Models.TunnelState.Error => "错误",
        _ => "未连接",
    };

    private void SubscribeState()
    {
        // 简化：周期刷新托盘工具提示文本。
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => UpdateIcon();
        timer.Start();
    }

    private void UpdateIcon()
    {
        // 状态通过工具提示文本呈现，图标统一使用 app.ico。
        var names = string.Join(", ", _main.Tunnels.Where(t => t.State == Core.Models.TunnelState.Connected).Select(t => t.Name));
        _taskbarIcon.ToolTipText = string.IsNullOrEmpty(names) ? "SSHTunnelProxy — 未连接" : $"SSHTunnelProxy — 已连接：{names}";
    }

    public void Dispose()
    {
        _taskbarIcon.Dispose();
    }
}
