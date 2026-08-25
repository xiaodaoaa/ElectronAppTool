using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.Net;
using System.Net.Sockets;

namespace SSHTunnelProxy.Core.Tunnel;

/// <summary>
/// 通过 SSH direct-tcpip Channel 转发到目标地址。
///
/// SSH.NET 2026 将低层 direct-tcpip Channel API 设为 internal，
/// 故采用公开的 <see cref="ForwardedPortLocal"/>（SSH.NET 官方动态端口
/// 转发机制）实现：在本地临时端口建立转发，再以 TcpClient 桥接，对外提供 Stream。
/// </summary>
public sealed class SshDirectTcpipChannel : IAsyncDisposable
{
    private readonly SshClient _client;
    private readonly ForwardedPortLocal _forwardedPort;
    private readonly TcpClient _localClient;
    private readonly ILogger? _logger;

    private SshDirectTcpipChannel(
        SshClient client,
        ForwardedPortLocal forwardedPort,
        TcpClient localClient,
        ILogger? logger)
    {
        _client = client;
        _forwardedPort = forwardedPort;
        _localClient = localClient;
        _logger = logger;
    }

    /// <summary>经本地桥接后可双向读写的对流（连接到 SSH 隧道）。</summary>
    public Stream Stream => _localClient.GetStream();

    /// <summary>
    /// 建立到目标地址的 direct-tcpip 转发，并返回可用于双向通信的流。
    /// </summary>
    /// <param name="client">已连接的 SSH 客户端</param>
    /// <param name="targetHost">目标主机</param>
    /// <param name="targetPort">目标端口</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="logger">运行日志器（可为 null）</param>
    public static async Task<SshDirectTcpipChannel> OpenAsync(
        SshClient client,
        string targetHost,
        int targetPort,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        // boundPort=0 使用系统分配的临时端口；本地仅本机回环访问。
        var forwardedPort = new ForwardedPortLocal(
            IPAddress.Loopback.ToString(),
            0,
            targetHost,
            (uint)targetPort);

        client.AddForwardedPort(forwardedPort);
        try
        {
            await Task.Run(() =>
            {
                forwardedPort.Start();
                _ = forwardedPort.BoundPort; // 触发端口绑定完成
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "ForwardedPortLocal 启动失败 {Target}", $"{targetHost}:{targetPort}");
            client.RemoveForwardedPort(forwardedPort);
            throw;
        }

        var localClient = new TcpClient { NoDelay = true };
        try
        {
            await localClient.ConnectAsync(
                IPAddress.Loopback,
                (int)forwardedPort.BoundPort,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "连接本地桥接端口失败 {Target}", $"{targetHost}:{targetPort}");
            localClient.Dispose();
            StopPort(client, forwardedPort, logger);
            throw;
        }

        return new SshDirectTcpipChannel(client, forwardedPort, localClient, logger);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        try
        {
            _localClient.Close();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "关闭本地桥接客户端异常");
        }
        StopPort(_client, _forwardedPort, _logger);
    }

    private static void StopPort(SshClient client, ForwardedPortLocal port, ILogger? logger)
    {
        try
        {
            port.Stop();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "停止 ForwardedPortLocal 异常");
        }
        try
        {
            client.RemoveForwardedPort(port);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "移除 ForwardedPortLocal 异常");
        }
        try
        {
            port.Dispose();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "释放 ForwardedPortLocal 异常");
        }
    }
}
