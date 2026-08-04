using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NtpTool.App.Services;
using NtpTool.Core.Logging;
using NtpTool.Core.Services;

namespace NtpTool.App;

/// <summary>
/// 单实例辅助：激活已有实例的主窗口。
/// </summary>
internal static class SingleInstance
{
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void ActivateExisting(string windowTitle)
    {
        var hwnd = FindWindow(null, windowTitle);
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }
}

/// <summary>
/// 应用入口。按需求文档第 8.1 节流程：加载配置 → 初始化日志 → 组装 DI →
/// 创建主窗口 → 检查权限 → 按配置启动服务端与自动同步。
/// 支持系统托盘：关闭窗口时最小化到托盘，托盘"退出"才真正结束进程。
/// </summary>
public partial class App : Application
{
    private static readonly Guid AppGuid = Guid.Parse("C5B3F1A0-4E2D-4A8B-9C7F-6D1E3A5B8C0D");
    private static readonly string MutexName = $"NtpTool-{AppGuid:N}";
    private Mutex? _singleInstanceMutex;
    private IServiceProvider? _serviceProvider;
    private ISystemTimeService? _systemTime;
    private IDisposable? _mainViewModelDisposable;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private bool _allowRealExit;
    private bool _allowCloseToTray = true;

    private readonly string _configPath = System.IO.Path.Combine(
        AppContext.BaseDirectory,
        Core.Models.AppSettings.DefaultFileName);

    private static System.Drawing.Icon LoadAppIcon()
    {
        var asm = typeof(App).Assembly;
        using var stream = asm.GetManifestResourceStream("NtpTool.App.app.ico");
        if (stream is not null)
            return new System.Drawing.Icon(stream);
        return System.Drawing.SystemIcons.Application;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例检查：如果已有实例在运行，激活其窗口后退出
        if (!TryAcquireSingleInstance())
        {
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var configRepository = new Infrastructure.Config.JsonConfigurationRepository(_configPath);
            var settings = configRepository.Load();
            _serviceProvider = CompositionRoot.Build(settings, _configPath);

            _systemTime = _serviceProvider.GetRequiredService<ISystemTimeService>();
            var logger = _serviceProvider.GetRequiredService<IAppLogger>();

            _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            _mainWindow = new MainWindow(_mainViewModel);
            _mainViewModelDisposable = _mainViewModel;
            _mainWindow.Closing += OnMainWindowClosing;
            MainWindow = _mainWindow;
            _mainWindow.Icon = LoadWindowIcon();
            _mainWindow.Show();

            SetupTray(_mainWindow, _mainViewModel);

            StartAccordingToConfig(logger);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用启动失败：{ex.Message}", "NTP TimeSync Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isNew);
            if (isNew)
                return true;
        }
        catch (AbandonedMutexException)
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out _);
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        SingleInstance.ActivateExisting("NTP TimeSync Tool");
        Shutdown();
        return false;
    }

    private void SetupTray(MainWindow window, MainViewModel viewModel)
    {
        var client = _serviceProvider!.GetRequiredService<INtpClientService>();
        var server = _serviceProvider!.GetRequiredService<INtpServerService>();

        _trayIcon = new TrayIcon(LoadAppIcon(),
            showWindow: ShowMainWindow,
            exitApp: ExitApplication);

        _trayIcon.SyncRequested += (_, _) => _ = client.SyncNowAsync();
        _trayIcon.BindServerActions(
            toggleServer: () =>
            {
                if (server.State == ServerState.Listening)
                {
                    _ = server.StopAsync();
                }
                else
                {
                    _ = server.StartAsync();
                }
            },
            isServerRunning: () => server.State == ServerState.Listening,
            confirm: (title, message) =>
                MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK);

        // 服务端状态变化时刷新托盘菜单项标签
        server.StateChanged += (_, _) => _trayIcon?.RefreshServerMenuItem();

        viewModel.ShowBalloonRequested += (_, message) =>
        {
            _trayIcon?.ShowBalloon("NTP TimeSync Tool", message);
        };

        // 托盘菜单"设置"与主界面"打开配置"都打开可视化设置窗口
        _trayIcon.SettingsRequested += (_, _) => OpenSettings();
        viewModel.SettingsRequested += (_, _) => OpenSettings();
    }

    /// <summary>打开设置窗口（模态），设置保存后自动应用到运行中的服务。</summary>
    private void OpenSettings()
    {
        var settingsService = _serviceProvider!.GetRequiredService<Services.SettingsService>();
        var owner = (_mainWindow is { IsVisible: true }) ? _mainWindow : null;
        settingsService.Open(owner);
        _mainViewModel?.RefreshFromSettings();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    /// <summary>
    /// 点击主界面关闭按钮时最小化到托盘（隐藏窗口），仅在明确退出时才真正关闭。
    /// </summary>
    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowRealExit || !_allowCloseToTray)
        {
            return;
        }

        // 取消关闭，最小化到托盘
        e.Cancel = true;
        _mainWindow?.Hide();
        _trayIcon?.ShowBalloon("NTP TimeSync Tool", "程序已最小化到系统托盘，双击图标可重新打开。", timeoutMs: 2500);
    }

    private void ExitApplication()
    {
        _allowRealExit = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _mainWindow?.Close();
        Shutdown();
    }

    private System.Windows.Media.Imaging.BitmapImage LoadWindowIcon()
    {
        var asm = typeof(App).Assembly;
        using var stream = asm.GetManifestResourceStream("NtpTool.App.app.ico");
        if (stream is not null)
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        return null!;
    }

    private void StartAccordingToConfig(IAppLogger logger)
    {
        var client = _serviceProvider!.GetRequiredService<INtpClientService>();
        var server = _serviceProvider!.GetRequiredService<INtpServerService>();
        var settings = _serviceProvider!.GetRequiredService<Core.Models.AppSettings>();

        logger.Information("App", $"NTP TimeSync Tool 启动，配置文件：{_configPath}");
        bool isAdmin = _systemTime!.IsAdministrator();
        logger.Information("App", isAdmin ? "以管理员权限运行。" : "以普通用户权限运行（修改系统时间或监听 123 端口需管理员权限）。");

        if (settings.Server.EnableServer)
        {
            _ = server.StartAsync();
        }
        else
        {
            logger.Information("App", "根据配置未启用 NTP Server，跳过自动启动。");
        }

        if (settings.Client.EnableAutoSync)
        {
            if (settings.Client.RunOnceOnStart)
            {
                _ = client.SyncNowAsync();
            }

            client.StartAutoSync();
        }
        else
        {
            logger.Information("App", "根据配置未启用自动同步，启动时不自动同步。");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _serviceProvider?.GetService<IAppLogger>()?.Fatal("App", $"未处理的异常：{e.Exception.Message}", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        _trayIcon?.Dispose();
        _mainViewModelDisposable?.Dispose();
        (_serviceProvider?.GetService<IAppLogger>() as IDisposable)?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}