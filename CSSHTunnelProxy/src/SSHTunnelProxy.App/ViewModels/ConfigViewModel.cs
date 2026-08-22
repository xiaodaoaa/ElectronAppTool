using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Security;

namespace SSHTunnelProxy.App.ViewModels;

/// <summary>
/// 服务器配置编辑对话框 ViewModel：新建或编辑一个 SSH 服务器配置。
/// </summary>
public partial class ConfigViewModel : ObservableObject
{
    private readonly IDpapiProtector _protector;
    private readonly SshServerProfile? _original;

    // --- SSH 服务器 ---
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private AuthMethod _authMethod = AuthMethod.Password;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _privateKeyPath = string.Empty;

    [ObservableProperty]
    private string _passphrase = string.Empty;

    // --- 本地代理 ---
    [ObservableProperty]
    private string _listenAddress = "127.0.0.1";

    [ObservableProperty]
    private int _socks5Port = 1080;

    [ObservableProperty]
    private int _httpPort = 8118;

    [ObservableProperty]
    private bool _enableProxyAuth;

    [ObservableProperty]
    private string _proxyUsername = string.Empty;

    [ObservableProperty]
    private string _proxyPassword = string.Empty;

    // --- 高级 ---
    [ObservableProperty]
    private int _connectTimeoutSec = 15;

    [ObservableProperty]
    private int _keepAliveIntervalSec = 30;

    [ObservableProperty]
    private int _maxReconnectAttempts = -1;

    [ObservableProperty]
    private int _reconnectDelaySec = 5;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>对话框是否以确定结果关闭（由视图监听并将其置为 true）。</summary>
    [ObservableProperty]
    private bool _result;

    public ConfigViewModel(IDpapiProtector protector, SshServerProfile? profile = null)
    {
        _protector = protector;
        _original = profile;

        if (profile is not null)
            LoadFromProfile(profile);
    }

    // --- 认证方式 UI 辅助 ---
    public bool IsPasswordAuth
    {
        get => AuthMethod == AuthMethod.Password;
        set { if (value) AuthMethod = AuthMethod.Password; }
    }

    public bool IsPrivateKeyAuth
    {
        get => AuthMethod == AuthMethod.PrivateKey;
        set { if (value) AuthMethod = AuthMethod.PrivateKey; }
    }

    public bool IsKeyboardAuth
    {
        get => AuthMethod == AuthMethod.KeyboardInteractive;
        set { if (value) AuthMethod = AuthMethod.KeyboardInteractive; }
    }

    public bool IsPasswordVisible => AuthMethod != AuthMethod.PrivateKey;
    public bool IsPrivateKeyVisible => AuthMethod == AuthMethod.PrivateKey;

    partial void OnAuthMethodChanged(AuthMethod value)
    {
        OnPropertyChanged(nameof(IsPasswordAuth));
        OnPropertyChanged(nameof(IsPrivateKeyAuth));
        OnPropertyChanged(nameof(IsKeyboardAuth));
        OnPropertyChanged(nameof(IsPasswordVisible));
        OnPropertyChanged(nameof(IsPrivateKeyVisible));
    }

    /// <summary>选择私钥文件。</summary>
    [RelayCommand]
    private void BrowseKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "私钥文件|*.*|PEM|*.pem|OpenSSH|*.ppk;*.openssh" };
        if (dialog.ShowDialog() == true)
            PrivateKeyPath = dialog.FileName;
    }

    private void LoadFromProfile(SshServerProfile p)
    {
        Name = p.Name;
        Host = p.Host;
        Port = p.Port;
        Username = p.Username;
        AuthMethod = p.AuthMethod;
        Password = _protector.Decrypt(p.EncryptedPassword);
        PrivateKeyPath = p.PrivateKeyPath;
        Passphrase = _protector.Decrypt(p.EncryptedPassphrase);
        ListenAddress = p.ListenAddress;
        Socks5Port = p.Socks5ListenPort;
        HttpPort = p.HttpListenPort;
        EnableProxyAuth = p.EnableProxyAuth;
        ProxyUsername = p.ProxyUsername;
        ProxyPassword = _protector.Decrypt(p.EncryptedProxyPassword);
        ConnectTimeoutSec = p.ConnectTimeoutSec;
        KeepAliveIntervalSec = p.KeepAliveIntervalSec;
        MaxReconnectAttempts = p.MaxReconnectAttempts;
        ReconnectDelaySec = p.ReconnectDelaySec;
    }

    /// <summary>根据当前输入构建/更新配置对象。</summary>
    public SshServerProfile BuildProfile()
    {
        var p = _original is null
            ? new SshServerProfile { Id = Guid.NewGuid() }
            : _original;

        p.Name = Name.Trim();
        p.Host = Host.Trim();
        p.Port = Port;
        p.Username = Username.Trim();
        p.AuthMethod = AuthMethod;
        p.EncryptedPassword = _protector.Encrypt(Password);
        p.PrivateKeyPath = PrivateKeyPath.Trim();
        p.EncryptedPassphrase = _protector.Encrypt(Passphrase);
        p.ListenAddress = ListenAddress.Trim();
        p.Socks5ListenPort = Socks5Port;
        p.HttpListenPort = HttpPort;
        p.EnableProxyAuth = EnableProxyAuth;
        p.ProxyUsername = ProxyUsername.Trim();
        p.EncryptedProxyPassword = _protector.Encrypt(ProxyPassword);
        p.ConnectTimeoutSec = ConnectTimeoutSec;
        p.KeepAliveIntervalSec = KeepAliveIntervalSec;
        p.MaxReconnectAttempts = MaxReconnectAttempts;
        p.ReconnectDelaySec = ReconnectDelaySec;
        return p;
    }

    [RelayCommand]
    private void ValidateAndConfirm()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "主机地址与用户名不能为空。";
            return;
        }
        if (AuthMethod == AuthMethod.Password && string.IsNullOrEmpty(Password))
        {
            StatusMessage = "密码认证需要填写密码。";
            return;
        }
        if (AuthMethod == AuthMethod.PrivateKey && string.IsNullOrEmpty(PrivateKeyPath))
        {
            StatusMessage = "私钥认证需要选择私钥文件。";
            return;
        }
        Result = true;
    }
}
