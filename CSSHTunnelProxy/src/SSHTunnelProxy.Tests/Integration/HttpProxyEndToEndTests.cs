using FluentAssertions;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Proxy;
using SSHTunnelProxy.Core.Tunnel;
using SSHTunnelProxy.Tests.Helpers;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace SSHTunnelProxy.Tests.Integration;

/// <summary>
/// HTTP 代理服务端到端集成测试（CONNECT 隧道模式）：
/// 验证 CONNECT 请求 → 隧道建立 → 双向透传 → 连接日志链路。
/// </summary>
public class HttpProxyEndToEndTests : IAsyncLifetime
{
    private readonly FakeSshTunnelTransport _transport = new();
    private readonly CollectingSink _sink = new();
    private readonly TrafficCounter _traffic = new();
    private HttpProxyServer? _server;
    private LocalTargetServer? _target;

    public async Task InitializeAsync()
    {
        await _transport.ConnectAsync();
    }

    public async Task DisposeAsync()
    {
        if (_server is not null)
            await _server.DisposeAsync();
        if (_target is not null)
            await _target.DisposeAsync();
        await _transport.DisposeAsync();
    }

    private async Task<HttpProxyServer> StartServerAsync(
        bool enableAuth = false,
        IProxyCredentialValidator? validator = null)
    {
        var server = new HttpProxyServer(_transport, new ProxyServerOptions
        {
            TunnelName = "HTTP集成测试",
            ListenAddress = "127.0.0.1",
            ListenPort = 0,
            EnableProxyAuth = enableAuth,
            CredentialValidator = validator,
            ConnectionSink = _sink,
            Traffic = _traffic,
        });
        await server.StartAsync();
        _server = server;
        return server;
    }

    [Fact]
    public async Task ConnectTunnel_EchoRoundtrip_And_Logs()
    {
        _target = LocalTargetServer.StartEcho();
        var server = await StartServerAsync();

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.BoundPort);
        using var stream = client.GetStream();

        // ① 发送 CONNECT 请求
        var connectLine = $"CONNECT 127.0.0.1:{_target.Port} HTTP/1.1\r\nHost: 127.0.0.1:{_target.Port}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(connectLine));

        // ② 读取 200 Connection Established
        var response = await ReadLineAsync(stream);
        response.Should().Be("HTTP/1.1 200 Connection Established");
        // 跳过其余响应头（\r\n\r\n）
        await DrainToHeaderEndAsync(stream);

        // ③ 通过隧道透传数据（回显验证）
        var message = "https-like-data-经隧道转发";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(message));
        var echo = new byte[Encoding.UTF8.GetByteCount(message)];
        await ReadExactlyAsync(stream, echo);
        Encoding.UTF8.GetString(echo).Should().Be(message);

        // 关闭客户端以触发服务端记录连接日志。
        client.Dispose();

        // ④ 连接日志
        var log = await _sink.WaitForAsync(l => l.TargetEndpoint == $"127.0.0.1:{_target.Port}");
        log.Status.Should().Be("Success");
        log.ProxyType.Should().Be(ProxyType.Http);
        log.BytesSent.Should().BeGreaterThan(0);
        log.BytesReceived.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Connect_WithoutAuth_WhenAuthRequired_Returns407()
    {
        _target = LocalTargetServer.StartEcho();
        var server = await StartServerAsync(
            enableAuth: true,
            validator: new FixedCredentialValidator { ExpectedUser = "alice", ExpectedPassword = "secret" });

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.BoundPort);
        using var stream = client.GetStream();

        var connectLine = $"CONNECT 127.0.0.1:{_target.Port} HTTP/1.1\r\nHost: 127.0.0.1:{_target.Port}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(connectLine));

        var line = await ReadLineAsync(stream);
        line.Should().StartWith("HTTP/1.1 407");

        var log = await _sink.WaitForAsync(l => l.Status == "Failed");
        log.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Connect_WithAuthHeader_Succeeds()
    {
        _target = LocalTargetServer.StartEcho();
        var server = await StartServerAsync(
            enableAuth: true,
            validator: new FixedCredentialValidator { ExpectedUser = "alice", ExpectedPassword = "secret" });

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.BoundPort);
        using var stream = client.GetStream();

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:secret"));
        var connectLine =
            $"CONNECT 127.0.0.1:{_target.Port} HTTP/1.1\r\n" +
            $"Host: 127.0.0.1:{_target.Port}\r\n" +
            $"Proxy-Authorization: Basic {basic}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(connectLine));

        var line = await ReadLineAsync(stream);
        line.Should().Be("HTTP/1.1 200 Connection Established");
        await DrainToHeaderEndAsync(stream);

        var message = "authenticated-tunnel-data";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(message));
        var echo = new byte[Encoding.UTF8.GetByteCount(message)];
        await ReadExactlyAsync(stream, echo);
        Encoding.UTF8.GetString(echo).Should().Be(message);
    }

    // ---- 辅助 ----

    private static async Task<string> ReadLineAsync(Stream stream)
    {
        var sb = new StringBuilder();
        var buf = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(buf);
            if (n == 0)
                throw new EndOfStreamException("连接被意外关闭。");
            if (buf[0] == '\n')
                break;
            if (buf[0] != '\r')
                sb.Append((char)buf[0]);
        }
        return sb.ToString();
    }

    private static async Task DrainToHeaderEndAsync(Stream stream)
    {
        // 依次读取剩余响应头行，直到空行（\r\n）为止。
        while (true)
        {
            var line = await ReadLineAsync(stream);
            if (line.Length == 0)
                return;
        }
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer[offset..]);
            if (n == 0)
                throw new EndOfStreamException("连接被意外关闭。");
            offset += n;
        }
    }
}
