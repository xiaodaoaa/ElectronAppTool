using System.Collections.ObjectModel;
using System.Windows;
using NtpTool.App.Mvvm;
using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.App;

/// <summary>
/// 设置窗口视图模型。基于 <see cref="AppSettings"/> 的深拷贝做编辑，
/// 保存前校验并规范化，持久化后通过 <see cref="SettingsSaved"/> 事件通知应用重载服务配置。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationRepository _repository;
    private readonly ConfigValidator _validator;
    private readonly AppSettings _current;
    private NtpServerConfig? _selectedServer;

    public AppSettings Edit { get; }

    /// <summary>服务器列表（可观察，供界面直接绑定并实时增删）。</summary>
    public ObservableCollection<NtpServerConfig> Servers { get; } = new();

    /// <summary>访问白名单（可观察，供界面直接绑定并实时增删）。</summary>
    public ObservableCollection<string> AllowedNetworks { get; } = new();

    public event EventHandler? SettingsSaved;

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand AddServerCommand { get; }
    public RelayCommand RemoveServerCommand { get; }
    public RelayCommand AddNetworkCommand { get; }
    public RelayCommand RemoveNetworkCommand { get; }

    /// <summary>服务器列表选中项（供删除）。</summary>
    public NtpServerConfig? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                RemoveServerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string? _selectedNetwork;

    /// <summary>白名单选中项（供删除）。</summary>
    public string? SelectedNetwork
    {
        get => _selectedNetwork;
        set
        {
            if (SetProperty(ref _selectedNetwork, value))
            {
                RemoveNetworkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _newNetwork = string.Empty;

    /// <summary>新增白名单条目的输入框文本（带通知，添加后清空能反馈到界面）。</summary>
    public string NewNetwork
    {
        get => _newNetwork;
        set => SetProperty(ref _newNetwork, value);
    }

    public SettingsViewModel(IConfigurationRepository repository, AppSettings current)
    {
        _repository = repository;
        _validator = new ConfigValidator();
        _current = current;
        Edit = current.Clone();

        foreach (var server in Edit.Client.Servers)
        {
            Servers.Add(server);
        }

        foreach (var network in Edit.Server.AllowedNetworks)
        {
            AllowedNetworks.Add(network);
        }

        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(ResetToDefaults);
        AddServerCommand = new RelayCommand(AddServer);
        RemoveServerCommand = new RelayCommand(RemoveServer, CanRemoveServer);
        AddNetworkCommand = new RelayCommand(AddNetwork);
        RemoveNetworkCommand = new RelayCommand(RemoveNetwork, CanRemoveNetwork);
    }

    private void AddServer()
    {
        var server = new NtpServerConfig
        {
            Host = "",
            Port = 123,
            Priority = Servers.Count == 0 ? 1 : Servers.Max(s => s.Priority) + 1,
            Enabled = true
        };
        Servers.Add(server);
        SelectedServer = server;
    }

    private bool CanRemoveServer() => SelectedServer is not null;

    private void RemoveServer()
    {
        if (SelectedServer is not null)
        {
            Servers.Remove(SelectedServer);
            SelectedServer = null;
        }
    }

    private void AddNetwork()
    {
        var value = (NewNetwork ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!ConfigValidator.IsValidNetwork(value))
        {
            MessageBox.Show(
                $"无效的地址或网段：{value}\n仅支持 IP（如 192.168.1.10）或 CIDR（如 192.168.1.0/24）。",
                "白名单校验",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (AllowedNetworks.Contains(value))
        {
            SelectedNetwork = value;
            NewNetwork = string.Empty;
            return;
        }

        AllowedNetworks.Add(value);
        SelectedNetwork = value;
        NewNetwork = string.Empty;
    }

    private void RemoveNetwork()
    {
        if (SelectedNetwork is not null)
        {
            AllowedNetworks.Remove(SelectedNetwork);
            SelectedNetwork = null;
        }
    }

    private void ResetToDefaults()
    {
        var defaults = new AppSettings();
        CopyInto(defaults, Edit);
        Servers.Clear();
        foreach (var server in Edit.Client.Servers)
        {
            Servers.Add(server);
        }
        AllowedNetworks.Clear();
        foreach (var network in Edit.Server.AllowedNetworks)
        {
            AllowedNetworks.Add(network);
        }
        SelectedServer = null;
        SelectedNetwork = null;

        // 触发 Edit 整体刷新，令所有 Edit.* 绑定（同步周期/端口/日志级别等）重新取值显示默认值
        OnPropertyChanged(nameof(Edit));
    }

    private void Save()
    {
        // 将界面编辑的服务器与白名单列表同步回 Edit，供校验与持久化
        SyncServersToEdit();
        SyncNetworksToEdit();

        var validation = _validator.Validate(Edit);
        _validator.Normalize(Edit);

        if (!validation.IsValid)
        {
            string message = string.Join("\n", validation.Errors);
            MessageBox.Show($"配置存在问题：\n{message}", "配置校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_repository.Save(Edit, out string? error))
        {
            MessageBox.Show($"保存配置失败：{error}", "保存配置", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CopyInto(Edit, _current);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
        MessageBox.Show("配置已保存并应用。", "配置", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>将界面可观察集合 <see cref="Servers"/> 同步回 <see cref="Edit"/> 的列表。</summary>
    private void SyncServersToEdit()
    {
        Edit.Client.Servers.Clear();
        foreach (var server in Servers)
        {
            Edit.Client.Servers.Add(server);
        }
    }

    /// <summary>将界面可观察集合 <see cref="AllowedNetworks"/> 同步回 <see cref="Edit"/> 的列表。</summary>
    private void SyncNetworksToEdit()
    {
        Edit.Server.AllowedNetworks.Clear();
        foreach (var network in AllowedNetworks)
        {
            Edit.Server.AllowedNetworks.Add(network);
        }
    }

    private bool CanRemoveNetwork() => SelectedNetwork is not null;

    /// <summary>将 <paramref name="source"/> 深拷贝到 <paramref name="target"/>。</summary>
    private static void CopyInto(AppSettings source, AppSettings target)
    {
        // 直接整体替换由 source 的 Clone 得到的当前快照
        var clone = source.Clone();
        target.Client = clone.Client;
        target.Server = clone.Server;
        target.Log = clone.Log;
    }
}