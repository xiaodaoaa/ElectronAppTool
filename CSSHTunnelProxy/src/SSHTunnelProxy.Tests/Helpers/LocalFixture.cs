using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SSHTunnelProxy.Tests.Helpers;

/// <summary>
/// 本地 TCP 目标服务器：模拟被代理的真实服务端。
/// 可配置为"回显"（Echo）或"写入固定响应"（Http）两种应答方式。
/// </summary>
public sealed class LocalTargetServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly byte[]? _fixedResponse;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _clients = new();
    private bool _running;

    public int Port { get; }

    private LocalTargetServer(byte[]? fixedResponse, int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _fixedResponse = fixedResponse;
        _running = true;
        _ = AcceptLoopAsync();
    }

    /// <summary>创建一个回显服务器：把收到的字节原样写回。</summary>
    public static LocalTargetServer StartEcho() => new(fixedResponse: null, port: 0);

    /// <summary>创建一个固定响应服务器：每收到一次数据就写回固定字节。</summary>
    public static LocalTargetServer StartWithResponse(string response) =>
        new(fixedResponse: Encoding.UTF8.GetBytes(response), port: 0);

    private async Task AcceptLoopAsync()
    {
        while (_running && !_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                break;
            }
            _clients.Add(HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            if (_fixedResponse is not null)
            {
                // 按"行"读取直到连接结束，每收到一批数据写回固定响应。
                var buffer = new byte[4096];
                while (true)
                {
                    var n = await stream.ReadAsync(buffer, _cts.Token);
                    if (n <= 0)
                        break;
                    await stream.WriteAsync(_fixedResponse, _cts.Token);
                    // 模拟服务端在响应后关闭写方向（HTTP 场景）。
                    if (_fixedResponse is { Length: > 0 } &&
                        buffer.AsSpan(0, n).IndexOf("GET "u8) >= 0)
                    {
                        // 保持连接打开，交由代理层决定何时关闭。
                    }
                }
            }
            else
            {
                // Echo：原样回写。
                await stream.CopyToAsync(stream, _cts.Token);
            }
        }
        catch
        {
            // 客户端断开即可，忽略。
        }
        finally
        {
            try { client.Dispose(); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _running = false;
        _cts.Cancel();
        _listener.Stop();
        try { await Task.WhenAll(_clients); } catch { }
        _cts.Dispose();
    }
}
