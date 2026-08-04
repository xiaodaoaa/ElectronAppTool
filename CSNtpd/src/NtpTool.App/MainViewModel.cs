using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using NtpTool.App.Mvvm;
using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.App;

/// <summary>
/// 主窗口视图模型。负责本地/UTC时间刷新、客户端与服务端状态绑定、
/// 操作命令、日志面板展示。对应需求文档第 5.1 节。
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAppLogger _logger;
    private readonly INtpClientService _client;
    private readonly INtpServerService _server;
    private readonly IConfigurationRepository _configRepository;
    private readonly ISystemTimeService _systemTime;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _clockTimer;

    private DateTime _localTime;
    private DateTime _utcTime;
    private string _clientStateText = "停止";
    private string _serverStateText = "停止";
    private string _clientStateColor = "#9D9D9D";
    private string _serverStateColor = "#9D9D9D";
    private string _lastSyncTime = "-";
    private string _upstreamServer = "-";
    private string _offsetText = "-";
    private string _delayText = "-";
    private long _totalRequests;
    private long _validRequests;
    private long _invalidRequests;
    private long _rejectedRequests;
    private string _recentClient = "-";
    private string _serverListenInfo = "-";
    private bool _isAdmin;

    public ObservableCollection<LogItem> LogItems { get; } = new();

    /// <summary>请求在系统托盘显示气泡通知（由 App 订阅并转交 TrayIcon）。</summary>
    public event EventHandler<string>? ShowBalloonRequested;

    /// <summary>请求打开可视化设置窗口（由 App 订阅并转交 SettingsService）。</summary>
    public event EventHandler? SettingsRequested;

    public MainViewModel(
        IAppLogger logger,
        INtpClientService client,
        INtpServerService server,
        IConfigurationRepository configRepository,
        ISystemTimeService systemTime,
        AppSettings settings,
        Dispatcher dispatcher)
    {
        _logger = logger;
        _client = client;
        _server = server;
        _configRepository = configRepository;
        _systemTime = systemTime;
        _settings = settings;
        _dispatcher = dispatcher;

        _client.StateChanged += OnClientStateChanged;
        _client.SyncCompleted += OnSyncCompleted;
        _server.StateChanged += OnServerStateChanged;
        _server.StatisticsChanged += OnStatisticsChanged;
        _logger.EntryWritten += OnLogWritten;

        SyncNowCommand = new AsyncRelayCommand(() => _client.SyncNowAsync());
        Func<bool> isAutoSyncRunning = () => _client.State != ClientSyncState.Stopped;
        StartAutoSyncCommand = new RelayCommand(_client.StartAutoSync, () => !isAutoSyncRunning());
        StopAutoSyncCommand = new RelayCommand(_client.StopAutoSync, isAutoSyncRunning);
        StartServerCommand = new AsyncRelayCommand(async () => await _server.StartAsync(), () => _server.State != ServerState.Listening);
        StopServerCommand = new AsyncRelayCommand(async () => await _server.StopAsync(), () => _server.State == ServerState.Listening);
        OpenConfigCommand = new RelayCommand(OpenConfig);
        ClearLogCommand = new RelayCommand(() => LogItems.Clear());
        ApplySystemTimeCommand = new AsyncRelayCommand(ApplySystemTimeManually);

        RefreshAll();

        _clockTimer = new DispatcherTimer(DispatcherPriority.Render, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => RefreshClock();
        _clockTimer.Start();
    }

    public string LocalTimeText => _localTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string UtcTimeText => _utcTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string ClientStateText { get => _clientStateText; private set => SetProperty(ref _clientStateText, value); }
    public string ServerStateText { get => _serverStateText; private set => SetProperty(ref _serverStateText, value); }
    public string ClientStateColor { get => _clientStateColor; private set => SetProperty(ref _clientStateColor, value); }
    public string ServerStateColor { get => _serverStateColor; private set => SetProperty(ref _serverStateColor, value); }
    public string LastSyncTime { get => _lastSyncTime; private set => SetProperty(ref _lastSyncTime, value); }
    public string UpstreamServer { get => _upstreamServer; private set => SetProperty(ref _upstreamServer, value); }
    public string OffsetText { get => _offsetText; private set => SetProperty(ref _offsetText, value); }
    public string DelayText { get => _delayText; private set => SetProperty(ref _delayText, value); }
    public long TotalRequests { get => _totalRequests; private set => SetProperty(ref _totalRequests, value); }
    public long ValidRequests { get => _validRequests; private set => SetProperty(ref _validRequests, value); }
    public long InvalidRequests { get => _invalidRequests; private set => SetProperty(ref _invalidRequests, value); }
    public long RejectedRequests { get => _rejectedRequests; private set => SetProperty(ref _rejectedRequests, value); }
    public string RecentClient { get => _recentClient; private set => SetProperty(ref _recentClient, value); }
    public string ServerListenInfo { get => _serverListenInfo; private set => SetProperty(ref _serverListenInfo, value); }
    public bool IsAdmin { get => _isAdmin; private set => SetProperty(ref _isAdmin, value); }
    public string AdminStatusText => IsAdmin ? "管理员" : "普通用户";

    public AsyncRelayCommand SyncNowCommand { get; }
    public RelayCommand StartAutoSyncCommand { get; }
    public RelayCommand StopAutoSyncCommand { get; }
    public AsyncRelayCommand StartServerCommand { get; }
    public AsyncRelayCommand StopServerCommand { get; }
    public RelayCommand OpenConfigCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public AsyncRelayCommand ApplySystemTimeCommand { get; }

    private void RefreshAll()
    {
        IsAdmin = _systemTime.IsAdministrator();
        UpstreamServer = FormatUpstream();
        ServerListenInfo = $"{_settings.Server.ListenAddress}:{_settings.Server.Port}";
        RefreshClock();
        RefreshStatistics();
        RefreshClientState(_client.State);
        RefreshServerState(_server.State);
    }

    /// <summary>设置保存后刷新界面显示的服务器与监听信息。</summary>
    public void RefreshFromSettings()
    {
        UpstreamServer = FormatUpstream();
        ServerListenInfo = $"{_settings.Server.ListenAddress}:{_settings.Server.Port}";
    }

    private string FormatUpstream()
    {
        NtpServerConfig? first = _settings.Client.Servers
            .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Host))
            .OrderBy(s => s.Priority)
            .FirstOrDefault();
        return first is null ? "-" : $"{first.Host}:{first.Port}";
    }

    private void RefreshClock()
    {
        _localTime = _systemTime.GetLocalNow();
        _utcTime = _systemTime.GetUtcNow();
        OnPropertyChanged(nameof(LocalTimeText));
        OnPropertyChanged(nameof(UtcTimeText));
    }

    private void RefreshStatistics()
    {
        var stats = _server.Statistics;
        TotalRequests = stats.TotalRequests;
        ValidRequests = stats.ValidRequests;
        InvalidRequests = stats.InvalidRequests;
        RejectedRequests = stats.RejectedRequests;
        RecentClient = stats.LastClientAddress ?? "-";
    }

    private void OnClientStateChanged(object? sender, ClientSyncState state)
        => RunOnUiThread(() =>
        {
            RefreshClientState(state);
            StartAutoSyncCommand.RaiseCanExecuteChanged();
            StopAutoSyncCommand.RaiseCanExecuteChanged();
        });

    private void RefreshClientState(ClientSyncState state)
    {
        ClientStateText = state switch
        {
            ClientSyncState.Stopped => "停止",
            ClientSyncState.Idle => "运行中",
            ClientSyncState.Syncing => "同步中",
            ClientSyncState.Success => "成功",
            ClientSyncState.Failed => "失败",
            ClientSyncState.Warning => "告警",
            _ => "未知"
        };
        ClientStateColor = state switch
        {
            ClientSyncState.Stopped => "#9D9D9D",
            ClientSyncState.Idle => "#0F6CBD",
            ClientSyncState.Syncing => "#FCE100",
            ClientSyncState.Success => "#13A10E",
            ClientSyncState.Failed or ClientSyncState.Warning => "#E81123",
            _ => "#9D9D9D"
        };
    }

    private void OnServerStateChanged(object? sender, ServerState state)
        => RunOnUiThread(() =>
        {
            RefreshServerState(state);
            RefreshStatistics();
            StartServerCommand.RaiseCanExecuteChanged();
            StopServerCommand.RaiseCanExecuteChanged();
        });

    private void OnStatisticsChanged(object? sender, EventArgs e)
        => RunOnUiThread(RefreshStatistics);

    private void RefreshServerState(ServerState state)
    {
        ServerStateText = state switch
        {
            ServerState.Stopped => "停止",
            ServerState.Starting => "启动中",
            ServerState.Listening => "监听中",
            ServerState.Error => "错误",
            _ => "未知"
        };
        ServerStateColor = state switch
        {
            ServerState.Stopped => "#9D9D9D",
            ServerState.Starting => "#0F6CBD",
            ServerState.Listening => "#13A10E",
            ServerState.Error => "#E81123",
            _ => "#9D9D9D"
        };
    }

    private void OnSyncCompleted(object? sender, NtpSyncResult result)
        => RunOnUiThread(() =>
        {
            if (result.Success)
            {
                LastSyncTime = result.SyncTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                UpstreamServer = result.Server;
                OffsetText = $"{result.OffsetMs:+0.00;-0.00} ms";
                DelayText = $"{result.RoundTripDelayMs:0.00} ms";
            }
            else
            {
                OffsetText = "-";
                DelayText = "-";
                ShowBalloonRequested?.Invoke(this,
                    $"时间同步失败：{result.ErrorMessage ?? "未知错误"}（服务器 {result.Server}）");
            }
        });

    private void OnLogWritten(object? sender, LogEntry entry)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            LogItems.Add(new LogItem
            {
                Time = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"),
                Level = entry.Level.ToShortName(),
                Module = entry.Module,
                Message = entry.Message
            });

            while (LogItems.Count > 2000)
            {
                LogItems.RemoveAt(0);
            }
        });
    }

    /// <summary>
    /// 将 UI 更新调度到界面线程执行。若当前已在界面线程则直接执行，
    /// 否则通过 <see cref="Dispatcher.Invoke"/> 同步切换到界面线程（阻塞等待），
    /// 确保事件处理在同一时刻按顺序完成。
    /// </summary>
    private void RunOnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }

    private void OpenConfig()
    {
        // 请求打开可视化设置窗口（由 App 处理并注入 SettingsService）
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task ApplySystemTimeManually()
    {
        var result = _client.LastResult;
        if (result is not { Success: true })
        {
            MessageBox.Show("尚无成功的同步结果，请先执行一次同步。", "修改系统时间", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_systemTime.IsAdministrator())
        {
            MessageBox.Show("修改系统时间需要管理员权限，请以管理员身份运行本程序。", "修改系统时间", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"是否将系统时间调整为同步结果？\n当前偏差：{result.OffsetMs:+0.00;-0.00} ms",
            "修改系统时间",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            DateTime target = _systemTime.GetLocalNow().AddMilliseconds(result.OffsetMs);
            _systemTime.SetLocalTime(target);
            _logger.Information("SystemTime", $"手动修改系统时间成功：→ {target:yyyy-MM-dd HH:mm:ss.fff}");
            MessageBox.Show("系统时间修改成功。", "修改系统时间", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (SystemTimeException ex)
        {
            _logger.Error("SystemTime", $"手动修改系统时间失败：{ex.Message}");
            MessageBox.Show(ex.Message, "修改系统时间", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _client.StateChanged -= OnClientStateChanged;
        _client.SyncCompleted -= OnSyncCompleted;
        _server.StateChanged -= OnServerStateChanged;
        _server.StatisticsChanged -= OnStatisticsChanged;
        _logger.EntryWritten -= OnLogWritten;
        _client.Dispose();
        _server.Dispose();
    }
}

/// <summary>UI 日志面板条目。</summary>
public sealed class LogItem
{
    public string Time { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}