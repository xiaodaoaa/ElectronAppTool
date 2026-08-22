namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// 代理服务器的公共配置。
/// </summary>
public sealed record ProxyServerOptions
{
    /// <summary>隧道名称（用于日志归属）。</summary>
    public string TunnelName { get; init; } = string.Empty;

    /// <summary>监听地址。</summary>
    public string ListenAddress { get; init; } = "127.0.0.1";

    /// <summary>监听端口（0 表示自动分配）。</summary>
    public int ListenPort { get; init; }

    /// <summary>是否启用代理层认证。</summary>
    public bool EnableProxyAuth { get; init; }

    /// <summary>代理认证校验器。</summary>
    public IProxyCredentialValidator? CredentialValidator { get; init; }

    /// <summary>连接日志接收器。</summary>
    public IConnectionSink? ConnectionSink { get; init; }

    /// <summary>流量计数器（可为 null）。</summary>
    public Tunnel.TrafficCounter? Traffic { get; init; }
}
