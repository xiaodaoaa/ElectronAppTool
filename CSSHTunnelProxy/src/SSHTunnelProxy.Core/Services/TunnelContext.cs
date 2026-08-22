using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Proxy;
using SSHTunnelProxy.Core.Tunnel;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 单个隧道实例的完整上下文：绑定服务器配置、SSH 传输、代理服务与流量计数。
/// </summary>
public sealed class TunnelContext : IAsyncDisposable
{
    public TunnelContext(
        SshServerProfile profile,
        ISshTunnelTransport transport,
        Socks5ProxyServer socks5Server,
        HttpProxyServer httpServer,
        TrafficCounter traffic)
    {
        Profile = profile;
        Transport = transport;
        Socks5Server = socks5Server;
        HttpServer = httpServer;
        Traffic = traffic;
    }

    /// <summary>服务器配置。</summary>
    public SshServerProfile Profile { get; }

    /// <summary>SSH 传输层。</summary>
    public ISshTunnelTransport Transport { get; }

    /// <summary>SOCKS5 代理服务器。</summary>
    public Socks5ProxyServer Socks5Server { get; }

    /// <summary>HTTP 代理服务器。</summary>
    public HttpProxyServer HttpServer { get; }

    /// <summary>流量计数器。</summary>
    public TrafficCounter Traffic { get; }

    /// <summary>隧道当前状态。</summary>
    public TunnelState State => Transport.State;

    public async ValueTask DisposeAsync()
    {
        await Socks5Server.DisposeAsync();
        await HttpServer.DisposeAsync();
        await Transport.DisposeAsync();
    }
}
