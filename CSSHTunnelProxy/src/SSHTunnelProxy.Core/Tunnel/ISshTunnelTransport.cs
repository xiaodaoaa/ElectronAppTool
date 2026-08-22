using SSHTunnelProxy.Core.Models;

namespace SSHTunnelProxy.Core.Tunnel;

/// <summary>
/// SSH 隧道传输接口：建立 SSH 连接并通过 direct-tcpip Channel 转发到目标地址。
/// </summary>
public interface ISshTunnelTransport : IAsyncDisposable
{
    /// <summary>当前隧道连接状态</summary>
    TunnelState State { get; }

    /// <summary>建立 SSH 连接。</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>断开 SSH 连接。</summary>
    Task DisconnectAsync();

    /// <summary>
    /// 通过 SSH 隧道连接到目标地址，返回双向流。
    /// 内部通过 direct-tcpip channel（ForwardedPortLocal）实现。
    /// </summary>
    /// <param name="targetHost">目标主机</param>
    /// <param name="targetPort">目标端口</param>
    Task<Stream> OpenChannelAsync(
        string targetHost,
        int targetPort,
        CancellationToken cancellationToken = default);

    /// <summary>流量统计更新事件</summary>
    event EventHandler<TrafficEventArgs>? TrafficUpdated;

    /// <summary>连接状态变更事件</summary>
    event EventHandler<TunnelStateEventArgs>? StateChanged;

    /// <summary>已建立的连接意外断开时触发（用于触发自动重连）。</summary>
    event EventHandler? ConnectionLost;
}
