using SSHTunnelProxy.App.ViewModels;
using SSHTunnelProxy.Core.Services;
using System.ComponentModel;
using System.Windows;

namespace SSHTunnelProxy.App.Views;

/// <summary>
/// 主窗口代码隐藏。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>是否正在退出应用（由托盘"退出"触发），此时关闭不再拦截。</summary>
    internal static bool IsQuitting;

    private readonly IConfigService _config;

    public MainWindow(MainViewModel viewModel, IConfigService config)
    {
        InitializeComponent();
        DataContext = viewModel;
        _config = config;

        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    /// <summary>恢复并前置主窗口（供托盘双击/菜单调用）。</summary>
    public void ShowFromTray()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // 设置即时生效：每次关闭时读取。
        var settings = _config.LoadSettingsAsync().GetAwaiter().GetResult();

        // 真正退出时放行；否则按"关闭时最小化到托盘"设置，把关闭转为隐藏到托盘。
        if (IsQuitting || !settings.CloseToTray)
            return;

        e.Cancel = true;
        Hide();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // 设置即时生效：每次状态变化时读取；最小化时收进托盘而不是任务栏。
        var settings = _config.LoadSettingsAsync().GetAwaiter().GetResult();
        if (settings.MinimizeToTray && WindowState == WindowState.Minimized)
            Hide();
    }
}
