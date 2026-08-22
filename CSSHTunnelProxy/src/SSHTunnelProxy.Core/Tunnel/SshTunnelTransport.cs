using Renci.SshNet;
using Renci.SshNet.Common;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Security;

namespace SSHTunnelProxy.Core.Tunnel;

/// <summary>
/// SSH 隧道传输实现：封装 SSH.NET 的 SshClient，负责连接建立、认证、
/// 主机密钥验证、保活，以及通过 direct-tcpip Channel 向目标转发。
/// </summary>
public sealed class SshTunnelTransport : ISshTunnelTransport
{
    private readonly SshServerProfile _profile;
    private readonly IHostKeyVerifier _hostKeyVerifier;
    private readonly IDpapiProtector _protector;
    private readonly object _stateLock = new();
    private SemaphoreSlim? _connectLock;

    private SshClient? _client;
    private CancellationTokenSource? _monitorCts;
    private volatile TunnelState _state = TunnelState.Disconnected;
    private object _monitorLock = new();

    public SshTunnelTransport(
        SshServerProfile profile,
        IHostKeyVerifier hostKeyVerifier,
        IDpapiProtector protector)
    {
        _profile = profile;
        _hostKeyVerifier = hostKeyVerifier;
        _protector = protector;
    }

    public TunnelState State
    {
        get
        {
            lock (_stateLock)
                return _state;
        }
    }

    public event EventHandler<TrafficEventArgs>? TrafficUpdated;

    public event EventHandler<TunnelStateEventArgs>? StateChanged;

    /// <summary>已建立的 SSH 连接意外断开时触发（用于触发自动重连）。</summary>
    public event EventHandler? ConnectionLost;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connectLock is null)
            _connectLock = new SemaphoreSlim(1, 1);

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is { IsConnected: true })
                return;

            SetState(TunnelState.Connecting);

            var connectionInfo = BuildConnectionInfo();
            var client = new SshClient(connectionInfo)
            {
                KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(1, _profile.KeepAliveIntervalSec)),
            };
            client.HostKeyReceived += OnHostKeyReceived;

            try
            {
                await client.ConnectAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                client.Dispose();
                SetState(TunnelState.Error, ex.Message);
                throw;
            }

            _client = client;
            SetState(TunnelState.Connected);
            client.ErrorOccurred += OnClientError;
            StartMonitor();
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        lock (_stateLock)
        {
            if (_state is TunnelState.Disconnected or TunnelState.Error)
            {
                var c = _client;
                _client = null;
                c?.Dispose();
                return;
            }
        }

        if (_connectLock is not null)
            await _connectLock.WaitAsync();
        try
        {
            var client = _client;
            _client = null;
            StopMonitor();
            if (client is not null)
            {
                client.HostKeyReceived -= OnHostKeyReceived;
                client.ErrorOccurred -= OnClientError;
                client.Disconnect();
                client.Dispose();
            }
            SetState(TunnelState.Disconnected);
        }
        finally
        {
            _connectLock?.Release();
        }
    }

    public async Task<Stream> OpenChannelAsync(
        string targetHost,
        int targetPort,
        CancellationToken cancellationToken = default)
    {
        var client = _client
            ?? throw new InvalidOperationException("SSH 隧道尚未连接。");

        if (!client.IsConnected)
            throw new IOException("SSH 连接已断开。");

        var channel = await SshDirectTcpipChannel.OpenAsync(
            client,
            targetHost,
            targetPort,
            cancellationToken);

        return channel.Stream;
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        e.CanTrust = _hostKeyVerifier.VerifyHostKey(_profile.Host, _profile.Port, e.HostKey);
    }

    private void OnClientError(object? sender, ExceptionEventArgs e)
    {
        // SSH 层错误，通常是连接中断。
        if (_state == TunnelState.Connected)
        {
            SetState(TunnelState.Error, e.Exception?.Message);
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>周期检查 SSH 连接是否仍存活，意外断开时触发重连信号。</summary>
    private void StartMonitor()
    {
        lock (_monitorLock)
        {
            StopMonitor();
            _monitorCts = new CancellationTokenSource();
            _ = MonitorLoopAsync(_monitorCts.Token);
        }
    }

    private void StopMonitor()
    {
        lock (_monitorLock)
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
        }
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                var client = _client;
                if (client is null || token.IsCancellationRequested)
                    break;
                if (_state == TunnelState.Connected && !client.IsConnected)
                {
                    SetState(TunnelState.Error, "SSH 连接已断开。");
                    ConnectionLost?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消。
        }
    }

    /// <summary>
    /// 根据配置构建 ConnectionInfo，选择认证方式。
    /// </summary>
    private ConnectionInfo BuildConnectionInfo()
    {
        var methods = new List<AuthenticationMethod>();

        switch (_profile.AuthMethod)
        {
            case AuthMethod.Password:
            {
                var password = _protector.Decrypt(_profile.EncryptedPassword);
                methods.Add(new PasswordAuthenticationMethod(_profile.Username, password));
                break;
            }
            case AuthMethod.PrivateKey:
            {
                var keyFiles = BuildPrivateKeyFiles();
                methods.Add(new PrivateKeyAuthenticationMethod(_profile.Username, keyFiles.ToArray()));
                break;
            }
            case AuthMethod.KeyboardInteractive:
            {
                var kia = new KeyboardInteractiveAuthenticationMethod(_profile.Username);
                kia.AuthenticationPrompt += OnKeyboardInteractive;
                methods.Add(kia);
                break;
            }
            default:
                throw new NotSupportedException($"不支持的认证方式：{_profile.AuthMethod}");
        }

        return new ConnectionInfo(
            _profile.Host,
            _profile.Port,
            _profile.Username,
            methods.ToArray())
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, _profile.ConnectTimeoutSec)),
        };
    }

    private List<PrivateKeyFile> BuildPrivateKeyFiles()
    {
        var files = new List<PrivateKeyFile>();

        if (!string.IsNullOrEmpty(_profile.EncryptedPrivateKeyContent))
        {
            // 内嵌私钥内容（已解密）。
            var content = _protector.Decrypt(_profile.EncryptedPrivateKeyContent);
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            var passphrase = _protector.Decrypt(_profile.EncryptedPassphrase);
            files.Add(string.IsNullOrEmpty(passphrase)
                ? new PrivateKeyFile(ms)
                : new PrivateKeyFile(ms, passphrase));
            return files;
        }

        var path = _profile.PrivateKeyPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var passphrase = _protector.Decrypt(_profile.EncryptedPassphrase);
            files.Add(string.IsNullOrEmpty(passphrase)
                ? new PrivateKeyFile(path)
                : new PrivateKeyFile(path, passphrase));
        }

        if (files.Count == 0)
            throw new InvalidOperationException("未找到可用的私钥。");

        return files;
    }

    private void OnKeyboardInteractive(object? sender, AuthenticationPromptEventArgs e)
    {
        foreach (var prompt in e.Prompts)
        {
            if (prompt.Request.Contains("password", StringComparison.OrdinalIgnoreCase))
                prompt.Response = _protector.Decrypt(_profile.EncryptedPassword);
        }
    }

    private void SetState(TunnelState newState, string? message = null)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = _state != newState;
            _state = newState;
        }
        if (changed)
            StateChanged?.Invoke(this, new TunnelStateEventArgs(newState, message));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
