using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SSHTunnelProxy.App.Framework;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace SSHTunnelProxy.App.ViewModels;

/// <summary>侧边栏导航页面。</summary>
public enum NavPage
{
    Tunnels,
    Logs,
    Settings,
}

/// <summary>
/// 主窗口 ViewModel：管理隧道列表、侧边栏导航、工具栏命令与实时流量刷新。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITunnelManager _manager;
    private readonly IConfigService _config;
    private readonly IServiceProvider _services;

    public ObservableCollection<TunnelItemViewModel> Tunnels { get; } = new();

    [ObservableProperty]
    private NavPage _currentPage = NavPage.Tunnels;

    [ObservableProperty]
    private TunnelItemViewModel? _selectedTunnel;

    [ObservableProperty]
    private string _connectedCountText = "无";

    private object? _currentPageContent;

    private readonly DispatcherTimer _trafficTimer;

    /// <summary>当前页内容（日志/设置页的 ViewModel）。</summary>
    public object? CurrentPageContent
    {
        get => _currentPageContent;
        private set => SetProperty(ref _currentPageContent, value);
    }

    public MainViewModel(
        ITunnelManager manager,
        IConfigService config,
        IServiceProvider services)
    {
        _manager = manager;
        _config = config;
        _services = services;

        _trafficTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _trafficTimer.Tick += (_, _) => RefreshTraffic();
        _trafficTimer.Start();

        LoadProfiles();
        CurrentPage = NavPage.Tunnels;

        // 启动后自动连接上次退出时仍处于已连接状态的隧道。
        _ = AutoConnectLastAsync();
    }

    /// <summary>
    /// 读取设置中记录的 LastConnectedProfileIds，自动连接对应隧道。
    /// 失败仅记录，不影响主窗口加载。
    /// </summary>
    private async Task AutoConnectLastAsync()
    {
        try
        {
            var settings = await _config.LoadSettingsAsync();
            if (settings.LastConnectedProfileIds is null || settings.LastConnectedProfileIds.Count == 0)
                return;

            // 按 ID 找到对应隧道项并启动；找不到的（已删除）跳过。
            var toConnect = Tunnels
                .Where(t => settings.LastConnectedProfileIds.Contains(t.Id))
                .ToList();

            foreach (var tunnel in toConnect)
                await tunnel.StartCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // 自动连接失败不应阻断应用启动，用户可手动连接。
        }
    }

    /// <summary>隧道页是否可见。</summary>
    public bool ShowTunnelPage => CurrentPage == NavPage.Tunnels;

    [RelayCommand]
    private void Navigate(NavPage page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(ShowTunnelPage));
        CurrentPageContent = page switch
        {
            NavPage.Logs => _services.GetRequiredService<LogViewModel>(),
            NavPage.Settings => _services.GetRequiredService<SettingsViewModel>(),
            _ => null,
        };
    }

    /// <summary>新建/编辑隧道配置，并保存回列表。</summary>
    [RelayCommand]
    private async Task NewTunnelAsync()
    {
        var dialog = new Views.ConfigDialog(_services);
        if (dialog.ShowDialog() == true && dialog.Result is SshServerProfile profile)
        {
            Tunnels.Add(new TunnelItemViewModel(profile, _manager));
            if (SelectedTunnel is null)
                SelectedTunnel = Tunnels[0];
            await PersistProfilesAsync();
        }
    }

    /// <summary>将当前隧道列表持久化到配置文件，确保重启后不丢失。</summary>
    private async Task PersistProfilesAsync()
    {
        try
        {
            var profiles = Tunnels.Select(t => t.Profile).ToList();
            await _config.SaveProfilesAsync(profiles);
        }
        catch (Exception)
        {
            // 持久化失败不应阻塞 UI；配置仅在本次会话内存中保留。
        }
    }

    /// <summary>删除指定隧道：确认后先断开运行中连接，再从列表移除并持久化。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DeleteTunnelAsync(TunnelItemViewModel? tunnel)
    {
        if (tunnel is null)
            return;

        var answer = MessageBox.Show(
            $"确定删除隧道 \"{tunnel.Name}\" 吗？此操作不可撤销。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        // 运行中（含连接中/重连中）先断开，复用现有 StopAsync 逻辑。
        if (tunnel.State is TunnelState.Connected or TunnelState.Connecting or TunnelState.Reconnecting)
            await tunnel.StopCommand.ExecuteAsync(null);

        Tunnels.Remove(tunnel);

        // 被删的若是当前选中项，回退到首项（空则为 null），避免详情面板悬空。
        if (SelectedTunnel == tunnel)
            SelectedTunnel = Tunnels.FirstOrDefault();

        await PersistProfilesAsync();
    }

    private bool CanDeleteTunnel(TunnelItemViewModel? tunnel) => tunnel is not null;

    /// <summary>编辑指定隧道：先断开运行中连接，复用对话框编辑模式，应用新配置并持久化。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task EditTunnelAsync(TunnelItemViewModel? tunnel)
    {
        if (tunnel is null)
            return;

        var wasRunning = tunnel.State is TunnelState.Connected
            or TunnelState.Connecting or TunnelState.Reconnecting;

        // 先断开运行中隧道再编辑：避免配置半更新竞争，且新端口/凭据需重启才生效。
        if (wasRunning)
            await tunnel.StopCommand.ExecuteAsync(null);

        // 复用 ConfigDialog 编辑模式：传 tunnel.Profile 即编辑现有配置。
        var dialog = new Views.ConfigDialog(_services, tunnel.Profile);
        if (dialog.ShowDialog() != true || dialog.Result is not SshServerProfile profile)
            return; // 取消：隧道已断开（若 wasRunning），配置未变。

        tunnel.ApplyProfile(profile);
        await PersistProfilesAsync();
    }

    private bool CanEditTunnel(TunnelItemViewModel? tunnel) => tunnel is not null;

    /// <summary>选中项变化时刷新删除/编辑命令的可用性。</summary>
    partial void OnSelectedTunnelChanged(TunnelItemViewModel? value)
    {
        DeleteTunnelCommand.NotifyCanExecuteChanged();
        EditTunnelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectTunnel(TunnelItemViewModel? tunnel) => SelectedTunnel = tunnel;

    private void LoadProfiles()
    {
        var profiles = _config.LoadProfilesAsync().GetAwaiter().GetResult();
        foreach (var p in profiles)
            Tunnels.Add(new TunnelItemViewModel(p, _manager));

        if (SelectedTunnel is null && Tunnels.Count > 0)
            SelectedTunnel = Tunnels[0];
    }

    private void RefreshTraffic()
    {
        // 所有隧道（含当前选中项）统一刷新一次即可。
        // 不能再单独调 SelectedTunnel.Tick()：Tick() 内部调用 counter.Sample()，
        // 该方法每次调用都会推进并清零一个采样桶。对选中项二次调用等于每秒清掉
        // 两个桶，稳态下窗口仅剩约 2/5 有效数据，显示速率被系统性压低到实际的 ~40%。
        foreach (var t in Tunnels)
            t.Tick();
        var connected = Tunnels.Count(t => t.State == Core.Models.TunnelState.Connected);
        ConnectedCountText = connected > 0 ? $"{connected} 个已连接" : "无";
    }
}
