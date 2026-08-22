namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// 本地代理服务器接口（SOCKS5 / HTTP 共用，便于统一启动/停止）。
/// </summary>
public interface IProxyServer : IAsyncDisposable
{
    /// <summary>代理类型。</summary>
    Models.ProxyType Type { get; }

    /// <summary>实际监听端口（0 表示尚未启动）。</summary>
    int BoundPort { get; }

    /// <summary>启动监听。</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>停止监听并关闭所有活跃连接。</summary>
    Task StopAsync();
}
