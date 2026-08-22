using Microsoft.Extensions.Logging;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Proxy;
using SSHTunnelProxy.Core.Security;
using SSHTunnelProxy.Core.Tunnel;
using System.Collections.Concurrent;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 隧道管理器实现：管理多隧道生命周期，并负责断线自动重连（指数退避）。
/// </summary>
public sealed class TunnelManager : ITunnelManager
{
    private readonly ConcurrentDictionary<Guid, TunnelContext> _tunnels = new();
    private readonly IHostKeyVerifier _hostKeyVerifier;
    private readonly IDpapiProtector _protector;
    private readonly IConnectionSink _connectionSink;
    private readonly ILogger<TunnelManager> _logger;
    private readonly Func<IHostKeyVerifier, IDpapiProtector, IConnectionSink, SshServerProfile, (ISshTunnelTransport Transport, IProxyCredentialValidator? Auth)> _transportFactory;

    public TunnelManager(
        IHostKeyVerifier hostKeyVerifier,
        IDpapiProtector protector,
        IConnectionSink connectionSink,
        ILogger<TunnelManager> logger,
        Func<IHostKeyVerifier, IDpapiProtector, IConnectionSink, SshServerProfile, (ISshTunnelTransport, IProxyCredentialValidator?)>? transportFactory = null)
    {
        _hostKeyVerifier = hostKeyVerifier;
        _protector = protector;
        _connectionSink = connectionSink;
        _logger = logger;
        _transportFactory = transportFactory ?? DefaultTransportFactory;
    }

    public event EventHandler<TunnelEventArgs>? TunnelStateChanged;

    public IReadOnlyCollection<TunnelContext> GetActiveTunnels()
    {
        return _tunnels.Values.ToArray();
    }

    public async Task<TunnelContext> StartTunnelAsync(SshServerProfile profile)
    {
        var (transport, auth) = _transportFactory(_hostKeyVerifier, _protector, _connectionSink, profile);

        var traffic = new TrafficCounter();
        var commonOptions = new ProxyServerOptions
        {
            TunnelName = profile.Name,
            ListenAddress = profile.ListenAddress,
            EnableProxyAuth = profile.EnableProxyAuth,
            CredentialValidator = auth,
            ConnectionSink = _connectionSink,
            Traffic = traffic,
        };

        var socks5 = new Socks5ProxyServer(transport, commonOptions with { ListenPort = profile.Socks5ListenPort });
        var http = new HttpProxyServer(transport, commonOptions with { ListenPort = profile.HttpListenPort });

        var context = new TunnelContext(profile, transport, socks5, http, traffic);
        _tunnels[profile.Id] = context;

        transport.StateChanged += (_, e) =>
            _ = Task.Run(() => TunnelStateChanged?.Invoke(this,
                new TunnelEventArgs(profile.Id, profile) { State = e.NewState }));

        try
        {
            RaiseState(profile.Id, profile, TunnelState.Connecting);
            await transport.ConnectAsync();
        }
        catch (Exception ex)
        {
            _tunnels.TryRemove(profile.Id, out _);
            RaiseState(profile.Id, profile, TunnelState.Error);
            _logger.LogError(ex, "隧道 {Tunnel} 连接失败", profile.Name);
            throw;
        }

        await socks5.StartAsync();
        await http.StartAsync();

        transport.ConnectionLost += async (_, _) =>
            await ReconnectLoopAsync(context, CancellationToken.None);

        RaiseState(profile.Id, profile, TunnelState.Connected);
        return context;
    }

    public async Task StopTunnelAsync(Guid tunnelId)
    {
        if (!_tunnels.TryGetValue(tunnelId, out var context))
            return;

        _tunnels.TryRemove(tunnelId, out _);
        RaiseState(tunnelId, context.Profile, TunnelState.Disconnected);
        await context.DisposeAsync();
    }

    public async Task RestartTunnelAsync(Guid tunnelId)
    {
        if (!_tunnels.TryGetValue(tunnelId, out var context))
            return;

        await StopTunnelAsync(tunnelId);
        await StartTunnelAsync(context.Profile);
    }

    public async Task StopAllAsync()
    {
        var ids = _tunnels.Keys.ToArray();
        foreach (var id in ids)
            await StopTunnelAsync(id);
    }

    private async Task ReconnectLoopAsync(TunnelContext context, CancellationToken externalCt)
    {
        var profile = context.Profile;
        var attempts = 0;
        var maxAttempts = profile.MaxReconnectAttempts; // -1 = 无限
        var delaySec = profile.ReconnectDelaySec;

        _logger.LogWarning("隧道 {Tunnel} 连接丢失，开始重连", profile.Name);
        RaiseState(profile.Id, profile, TunnelState.Reconnecting);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        while (maxAttempts == -1 || attempts < maxAttempts)
        {
            attempts++;
            try
            {
                await context.Transport.ConnectAsync(linkedCts.Token);
                RaiseState(profile.Id, profile, TunnelState.Connected);
                _logger.LogInformation("隧道 {Tunnel} 重连成功（第 {N} 次尝试）", profile.Name, attempts);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "隧道 {Tunnel} 重连失败（第 {N} 次）", profile.Name, attempts);
            }

            // 指数退避：5 → 10 → 20 → 40 → 60（封顶）。
            var wait = Math.Min(delaySec * (1 << Math.Min(attempts - 1, 4)), 60);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(wait), linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        RaiseState(profile.Id, profile, TunnelState.Error, "重连失败，已达最大尝试次数。");
    }

    private void RaiseState(Guid id, SshServerProfile? profile, TunnelState state, string? message = null)
        => TunnelStateChanged?.Invoke(this, new TunnelEventArgs(id, profile) { State = state });

    private static (ISshTunnelTransport, IProxyCredentialValidator?) DefaultTransportFactory(
        IHostKeyVerifier verifier, IDpapiProtector protector, IConnectionSink sink, SshServerProfile profile)
    {
        IProxyCredentialValidator? auth = null;
        if (profile.EnableProxyAuth)
            auth = new ProxyCredentialValidator(profile.ProxyUsername, protector.Decrypt(profile.EncryptedProxyPassword));

        var transport = new SshTunnelTransport(profile, verifier, protector);
        return (transport, auth);
    }
}
