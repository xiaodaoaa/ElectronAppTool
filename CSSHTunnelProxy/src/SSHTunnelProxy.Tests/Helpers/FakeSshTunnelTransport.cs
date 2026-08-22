using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Tunnel;
using System.Net.Sockets;

namespace SSHTunnelProxy.Tests.Helpers;

/// <summary>
/// 模拟 SSH 隧道传输：OpenChannelAsync 直接与目标建立真实 TCP 连接，
/// 用于在没有真实 SSH 服务端的环境下测试代理协议与转发链路。
/// </summary>
public sealed class FakeSshTunnelTransport : ISshTunnelTransport
{
    public TunnelState State { get; private set; } = TunnelState.Connected;

    public event EventHandler<TrafficEventArgs>? TrafficUpdated;
    public event EventHandler<TunnelStateEventArgs>? StateChanged;
    public event EventHandler? ConnectionLost;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        State = TunnelState.Connected;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        State = TunnelState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<Stream> OpenChannelAsync(
        string targetHost,
        int targetPort,
        CancellationToken cancellationToken = default)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(targetHost, targetPort, cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
