using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SSHTunnelProxy.App.Framework;
using SSHTunnelProxy.App.ViewModels;
using SSHTunnelProxy.App.Views;
using SSHTunnelProxy.Core;
using SSHTunnelProxy.Core.Services;
using SSHTunnelProxy.Core.Utils;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace SSHTunnelProxy.App;

/// <summary>
/// 应用入口：配置依赖注入、Serilog 日志与整体生命周期。
/// </summary>
public partial class App : Application
{
    private static readonly Guid AppGuid = Guid.Parse("7A2C4E8B-1D3F-4A9B-8C5E-6F0D2B7A4C9E");
    private static readonly string MutexName = $"SSHTunnelProxy-{AppGuid:N}";
    /// <summary>跨进程事件名：第二个实例通知首个实例"显示主窗口"。</summary>
    private static readonly string ShowEventName = $"SSHTunnelProxy-Show-{AppGuid:N}";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showRegistration;
    private volatile bool _shuttingDown;

    private ServiceProvider? _services;

    public static new App Current => (App)Application.Current;

    /// <summary>全局依赖注入服务。</summary>
    public IServiceProvider Services => _services!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例检查：若已有实例在运行，激活其主窗口后退出本实例。
        if (!TryAcquireSingleInstance())
        {
            return;
        }

        base.OnStartup(e);
        ConfigureSerilog();

        var services = new ServiceCollection();
        services.AddSSHTunnelProxyCore();
        // 注册 ILogger<>，桥接到静态 Serilog Log，保证 Core 层 ILogger<TunnelManager> 可解析。
        services.AddLogging(builder => builder.AddProvider(SerilogLoggerProvider.Instance));
        services.AddSingleton<MainViewModel>();
        services.AddTransient<LogViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<TrayIconController>(sp => new TrayIconController(
            sp.GetRequiredService<MainViewModel>(),
            sp.GetRequiredService<ITunnelManager>(),
            () => sp.GetRequiredService<MainWindow>()));

        _services = services.BuildServiceProvider();

        // 全局兜底：未处理异常记录日志并弹窗，避免静默崩溃。
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "未处理的 UI 异常");
            MessageBox.Show(
                $"发生未处理的异常：{args.Exception.Message}\n\n{args.Exception}",
                "SSHTunnelProxy", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        ApplyAccentColor();

        // 创建系统托盘（在窗口后，确保 UI 线程就绪）。
        _services.GetRequiredService<TrayIconController>();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;

        // 启动后最小化到托盘：不显示主窗口，直接隐藏到托盘。
        var settings = _services.GetRequiredService<IConfigService>().LoadSettingsAsync().GetAwaiter().GetResult();
        if (settings.StartMinimizedToTray)
            window.Hide();
        else
            window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shuttingDown = true;

        // 注销显示信号回调并释放命名事件。
        if (_showRegistration is not null)
        {
            _showRegistration.Unregister(null);
            _showRegistration = null;
        }
        _showEvent?.Dispose();
        _showEvent = null;

        // 退出时记录仍处于已连接状态的隧道，下次启动自动恢复连接。
        PersistLastConnectedTunnels();

        var tray = _services?.GetService<TrayIconController>();
        tray?.Dispose();
        Log.Information("应用退出");
        Log.CloseAndFlush();

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        base.OnExit(e);
    }

    /// <summary>
    /// 尝试获取单实例互斥锁。返回 true 表示当前实例是首个实例，可继续启动；
    /// 返回 false 表示已有实例在运行，已通知其显示主窗口并准备退出本实例。
    /// </summary>
    /// <remarks>
    /// 不使用 FindWindow 按标题查找窗口——隐藏在托盘的主窗口查找不可靠
    /// （有时找不到、有时误匹配 Explorer 文件夹窗口标题）。改用命名事件：
    /// 首个实例创建事件并注册回调，第二个实例 Set 事件通知首个实例自行显示窗口。
    /// </remarks>
    private bool TryAcquireSingleInstance()
    {
        if (!AcquireMutex())
        {
            // 已有实例在运行：通知其显示主窗口，然后退出本实例。
            NotifyExistingInstance();
            Shutdown();
            return false;
        }

        // 首个实例：创建命名事件，注册回调。第二个实例 Set 此事件时回调在
        // 线程池线程触发，封送到 UI 线程后从托盘恢复并前置主窗口。
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showEvent, OnShowSignal, state: null, Timeout.InfiniteTimeSpan, executeOnlyOnce: false);
        return true;
    }

    private bool AcquireMutex()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: false, MutexName, out var isNew);
            return isNew;
        }
        catch (AbandonedMutexException)
        {
            // 前一个实例异常终止遗留了互斥锁，本实例直接接管。
            _singleInstanceMutex = new Mutex(initiallyOwned: false, MutexName, out _);
            return true;
        }
    }

    /// <summary>通知已有实例显示主窗口：打开同名事件并 Set。</summary>
    private static void NotifyExistingInstance()
    {
        try
        {
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            showEvent.Set();
            showEvent.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"通知已有实例失败: {ex.Message}");
        }
    }

    /// <summary>命名事件的回调（线程池线程触发）：封送到 UI 线程，从托盘恢复并前置主窗口。</summary>
    private void OnShowSignal(object? state, bool timedOut)
    {
        if (_shuttingDown)
            return;

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (Application.Current?.MainWindow is Views.MainWindow w)
                w.ShowFromTray();
        });
    }

    /// <summary>
    /// 把当前已连接隧道的配置 ID 写入设置，供下次启动自动连接。
    /// </summary>
    private void PersistLastConnectedTunnels()
    {
        try
        {
            if (_services is null)
                return;

            var main = _services.GetRequiredService<MainViewModel>();
            var config = _services.GetRequiredService<IConfigService>();
            var settings = config.LoadSettingsAsync().GetAwaiter().GetResult();

            settings.LastConnectedProfileIds = main.Tunnels
                .Where(t => t.State == Core.Models.TunnelState.Connected)
                .Select(t => t.Id)
                .ToList();

            config.SaveSettingsAsync(settings).GetAwaiter().GetResult();
            Log.Information("已记录 {Count} 个隧道用于下次启动自动连接", settings.LastConnectedProfileIds.Count);
        }
        catch (Exception ex)
        {
            // 持久化失败不应阻断退出流程。
            Log.Warning(ex, "记录已连接隧道失败");
        }
    }

    private void ConfigureSerilog()
    {
        var logDir = Path.Combine(AppPaths.Root, "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50 * 1024 * 1024)
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information("SSHTunnelProxy 启动");
    }

    /// <summary>
    /// 读取系统强调色并注入到 Application.Resources 的配色键，覆盖默认占位色。
    /// 资源查找先查顶层再查 MergedDictionaries，因此注入值优先于配色里的占位色。
    /// </summary>
    private void ApplyAccentColor()
    {
        var argb = new AccentColorProvider().GetAccentArgb();
        var color = Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        Resources["AccentColor"] = color;
        Resources["AccentBrush"] = new SolidColorBrush(color);
        Resources["AccentHoverBrush"] = new SolidColorBrush(ShiftBrightness(color, 1.12f));
        Resources["AccentPressedBrush"] = new SolidColorBrush(ShiftBrightness(color, 0.85f));
    }

    private static Color ShiftBrightness(Color c, float factor)
        => Color.FromRgb(
            (byte)Math.Clamp(c.R * factor, 0, 255),
            (byte)Math.Clamp(c.G * factor, 0, 255),
            (byte)Math.Clamp(c.B * factor, 0, 255));
}
