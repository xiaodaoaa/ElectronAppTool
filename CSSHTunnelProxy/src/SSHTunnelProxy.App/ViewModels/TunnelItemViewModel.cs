using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSHTunnelProxy.App.Framework;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Services;

namespace SSHTunnelProxy.App.ViewModels;

/// <summary>
/// 隧道列表项：包装一个服务器配置，并提供连接/断开/重启控制与实时状态。
/// </summary>
public partial class TunnelItemViewModel : ObservableObject
{
    private readonly ITunnelManager _manager;
    private readonly DispatcherUI _ui;

    private TunnelContext? _context;

    public TunnelItemViewModel(SshServerProfile profile, ITunnelManager manager)
    {
        Profile = profile;
        _manager = manager;
        _ui = new DispatcherUI();
        State = TunnelState.Disconnected;
    }

    /// <summary>底层服务器配置。</summary>
    public SshServerProfile Profile { get; }

    public Guid Id => Profile.Id;
    public string Name => Profile.Name;
    public string ServerInfo => $"{Profile.Username}@{Profile.Host}:{Profile.Port}";
    public string PortsInfo => $"{Profile.Socks5ListenPort}(SOCKS5)  {Profile.HttpListenPort}(HTTP)";

    /// <summary>
    /// 应用编辑后的配置并刷新派生显示属性。
    /// Profile 为只读引用，此处逐字段复制（Id 不变——TunnelManager 字典键依赖它），
    /// 并手动触发 Name/ServerInfo/PortsInfo 变更通知（它们无自动 INPC）。
    /// </summary>
    public void ApplyProfile(SshServerProfile profile)
    {
        Profile.Name = profile.Name;
        Profile.Host = profile.Host;
        Profile.Port = profile.Port;
        Profile.Username = profile.Username;
        Profile.AuthMethod = profile.AuthMethod;
        Profile.EncryptedPassword = profile.EncryptedPassword;
        Profile.PrivateKeyPath = profile.PrivateKeyPath;
        Profile.EncryptedPassphrase = profile.EncryptedPassphrase;
        Profile.EncryptedPrivateKeyContent = profile.EncryptedPrivateKeyContent;
        Profile.ListenAddress = profile.ListenAddress;
        Profile.Socks5ListenPort = profile.Socks5ListenPort;
        Profile.HttpListenPort = profile.HttpListenPort;
        Profile.EnableProxyAuth = profile.EnableProxyAuth;
        Profile.ProxyUsername = profile.ProxyUsername;
        Profile.EncryptedProxyPassword = profile.EncryptedProxyPassword;
        Profile.ConnectTimeoutSec = profile.ConnectTimeoutSec;
        Profile.KeepAliveIntervalSec = profile.KeepAliveIntervalSec;
        Profile.MaxReconnectAttempts = profile.MaxReconnectAttempts;
        Profile.ReconnectDelaySec = profile.ReconnectDelaySec;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ServerInfo));
        OnPropertyChanged(nameof(PortsInfo));
    }

    [ObservableProperty]
    private TunnelState _state;

    [ObservableProperty]
    private string _statusMessage = "未连接";

    [ObservableProperty]
    private long _totalSent;

    [ObservableProperty]
    private long _totalReceived;

    [ObservableProperty]
    private double _uploadSpeed;

    [ObservableProperty]
    private double _downloadSpeed;

    [ObservableProperty]
    private string _uptimeText = "--";

    private DateTime _connectedAtUtc;

    /// <summary>启动隧道。</summary>
    [RelayCommand]
    private async Task StartAsync()
    {
        try
        {
            StatusMessage = "正在连接…";
            State = TunnelState.Connecting;

            // 若已存在运行上下文则先清理。
            if (_context is not null)
            {
                await _manager.StopTunnelAsync(Id);
                _context = null;
            }

            _context = await _manager.StartTunnelAsync(Profile);
            AttachEvents(_context);
            _connectedAtUtc = DateTime.UtcNow;
            // 显式设 State：transport 的 StateChanged(Connected) 在 ConnectAsync 内部
            // 已触发，早于 AttachEvents 订阅，事件已丢失，故首次连接成功必须手动设。
            State = TunnelState.Connected;
            StatusMessage = "已连接";
        }
        catch (Exception ex)
        {
            StatusMessage = $"连接失败：{ex.Message}";
            State = TunnelState.Error;
        }
    }

    /// <summary>停止隧道。</summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        try
        {
            await _manager.StopTunnelAsync(Id);
        }
        finally
        {
            _context = null;
            State = TunnelState.Disconnected;
            StatusMessage = "未连接";
        }
    }

    /// <summary>重启隧道。</summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        try
        {
            StatusMessage = "正在重启…";
            State = TunnelState.Connecting;

            // 复用 StartAsync 的"停止旧上下文 → 启动新上下文 → 重新订阅事件"流程，
            // 确保 _context、State、StatusMessage 与事件订阅全部一致。
            // 不能直接调 _manager.RestartTunnelAsync：它内部新建 transport，
            // 但新 transport 的 StateChanged 事件未接到本 ViewModel，会导致重连成功后
            // 圆点颜色与状态文字不更新（State 停在旧值）。
            if (_context is not null)
            {
                await _manager.StopTunnelAsync(Id);
                _context = null;
            }

            _context = await _manager.StartTunnelAsync(Profile);
            AttachEvents(_context);
            _connectedAtUtc = DateTime.UtcNow;
            State = TunnelState.Connected;
            StatusMessage = "已连接";
        }
        catch (Exception ex)
        {
            StatusMessage = $"重启失败：{ex.Message}";
            State = TunnelState.Error;
        }
    }

    private void AttachEvents(TunnelContext context)
    {
        context.Transport.StateChanged += (_, e) =>
            _ui.Run(() =>
            {
                State = e.NewState;
                StatusMessage = e.Message ?? (e.NewState == TunnelState.Connected ? "已连接" : "");
            });
        context.Transport.ConnectionLost += (_, _) => _ui.Run(() =>
        {
            StatusMessage = "连接断开，正在重连…";
            State = TunnelState.Reconnecting;
        });
    }

    /// <summary>由 UI 定时器驱动，刷新流量与运行时长。</summary>
    public void Tick()
    {
        var ctx = _context;
        if (ctx is null)
            return;

        var counter = ctx.Traffic;
        TotalSent = counter.TotalBytesSent;
        TotalReceived = counter.TotalBytesReceived;

        var (up, down) = counter.Sample();
        UploadSpeed = up;
        DownloadSpeed = down;

        if (State == TunnelState.Connected)
        {
            var elapsed = DateTime.UtcNow - _connectedAtUtc;
            UptimeText = $"{elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }
}
