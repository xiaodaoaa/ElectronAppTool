using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Tunnel;
using SSHTunnelProxy.Core.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// HTTP 代理服务器（首期仅支持 CONNECT 隧道模式，覆盖 HTTPS 场景）。
/// 普通 GET/POST 转发不在首期范围。
/// </summary>
public sealed class HttpProxyServer : IProxyServer
{
    private readonly ProxyServerOptions _options;
    private readonly ISshTunnelTransport _transport;

    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private volatile int _activeClients;
    private volatile bool _running;

    public HttpProxyServer(ISshTunnelTransport transport, ProxyServerOptions options)
    {
        _transport = transport;
        _options = options;
    }

    public ProxyType Type => ProxyType.Http;

    public int BoundPort { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            return;

        var address = IPAddress.Parse(_options.ListenAddress);
        _listener = new TcpListener(address, _options.ListenPort);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _running = true;

        _ = AcceptLoopAsync();
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_running)
            return;

        _running = false;
        _cts.Cancel();
        _listener?.Stop();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_activeClients > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        _listener = null;
    }

    private async Task AcceptLoopAsync()
    {
        while (_running && !_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                break;
            }

            Interlocked.Increment(ref _activeClients);
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        Stream? channel = null;
        var started = DateTime.UtcNow;
        var log = new ConnectionLog
        {
            Timestamp = DateTime.Now,
            TunnelName = _options.TunnelName,
            ProxyType = ProxyType.Http,
            ClientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? string.Empty,
        };

        _options.Traffic?.AddConnection();
        try
        {
            using var clientStream = client.GetStream();
            channel = await RunProtocolAsync(clientStream, log);
            log.Status = "Success";
        }
        catch (Exception)
        {
            log.Status = "Failed";
        }
        finally
        {
            _options.Traffic?.RemoveConnection();
            log.Duration = DateTime.UtcNow - started;

            if (channel is not null)
            {
                try { channel.Dispose(); } catch { }
            }

            var sink = _options.ConnectionSink;
            if (sink is not null)
            {
                try { await sink.RecordConnectionAsync(log); } catch { }
            }

            client.Dispose();
            Interlocked.Decrement(ref _activeClients);
        }
    }

    private async Task<Stream> RunProtocolAsync(Stream clientStream, ConnectionLog log)
    {
        // 读取请求头（直到 \r\n\r\n），限制最大头部大小以防恶意请求。
        var headBytes = await ReadHeadersAsync(clientStream, maxBytes: 64 * 1024);
        var request = HttpParser.ParseHeaders(headBytes)
            ?? throw new HttpParseException("未收到完整 HTTP 请求头。");

        if (!request.Method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(clientStream,
                "HTTP/1.1 405 Method Not Allowed\r\n" +
                "Content-Length: 0\r\nConnection: close\r\n\r\n");
            throw new HttpParseException($"首期仅支持 CONNECT 方法，收到：{request.Method}");
        }

        var (host, port) = HttpParser.ParseAuthority(request.ConnectTarget!);
        log.TargetEndpoint = $"{host}:{port}";

        // 代理认证（可选）：验证 Proxy-Authorization Basic。
        if (_options.EnableProxyAuth && _options.CredentialValidator is not null)
        {
            if (!TryValidateProxyAuth(request.Headers, out var failureReason))
            {
                await WriteResponseAsync(clientStream,
                    "HTTP/1.1 407 Proxy Authentication Required\r\n" +
                    "Proxy-Authenticate: Basic realm=\"SSHTunnelProxy\"\r\n" +
                    "Content-Length: 0\r\nConnection: close\r\n\r\n");
                log.Status = "Failed";
                throw new HttpParseException(failureReason);
            }
        }

        if (_transport.State != TunnelState.Connected)
        {
            await WriteResponseAsync(clientStream,
                "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            throw new IOException("SSH 隧道未连接。");
        }

        Stream channel;
        try
        {
            channel = await _transport.OpenChannelAsync(host, port, _cts.Token);
        }
        catch (Exception)
        {
            await WriteResponseAsync(clientStream,
                "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            throw;
        }

        await clientStream.WriteAsync(
            "HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(),
            _cts.Token);

        // 双向透传。
        var relay = await StreamRelay.RelayAsync(clientStream, channel, _options.Traffic, _cts.Token);
        log.BytesSent = relay.BytesUpstream;
        log.BytesReceived = relay.BytesDownstream;
        return channel;
    }

    private bool TryValidateProxyAuth(Dictionary<string, string> headers, out string reason)
    {
        reason = string.Empty;
        if (!headers.TryGetValue("Proxy-Authorization", out var auth) ||
            !auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            reason = "缺少代理认证头。";
            return false;
        }

        try
        {
            // Basic base64(user:pass)
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth[6..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep <= 0)
            {
                reason = "无效的认证格式。";
                return false;
            }
            var user = decoded[..sep];
            var pass = decoded[(sep + 1)..];
            return _options.CredentialValidator!.Validate(user, pass);
        }
        catch (FormatException)
        {
            reason = "无效的 Base64 认证。";
            return false;
        }
    }

    private static async Task<byte[]> ReadHeadersAsync(Stream stream, int maxBytes)
    {
        var buffer = new byte[maxBytes];
        var filled = 0;
        var buf = new byte[1024];
        while (true)
        {
            var n = await stream.ReadAsync(buf, CancellationToken.None);
            if (n == 0)
                throw new EndOfStreamException("客户端提前断开。");

            if (filled + n > maxBytes)
                throw new HttpParseException("HTTP 请求头过大。");
            buf.AsSpan(0, n).CopyTo(buffer.AsSpan(filled));
            filled += n;

            // 检测是否已收到空行。
            if (ContainsHeaderTerminator(buffer, filled))
                return buffer[..filled];
        }
    }

    private static bool ContainsHeaderTerminator(byte[] buffer, int filled)
    {
        if (filled < 4)
            return false;
        for (var i = 0; i <= filled - 4; i++)
        {
            if (buffer[i] == '\r' && buffer[i + 1] == '\n' &&
                buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                return true;
        }
        return false;
    }

    private static ValueTask WriteResponseAsync(Stream stream, string response)
        => stream.WriteAsync(Encoding.ASCII.GetBytes(response));

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
