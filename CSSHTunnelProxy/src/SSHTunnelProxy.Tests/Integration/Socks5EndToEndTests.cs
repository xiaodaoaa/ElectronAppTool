using FluentAssertions;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Proxy;
using SSHTunnelProxy.Core.Tunnel;
using SSHTunnelProxy.Tests.Helpers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace SSHTunnelProxy.Tests.Integration;

/// <summary>
/// SOCKS5 代理服务端到端集成测试：
/// 用 FakeSshTunnelTransport 模拟 SSH 隧道，验证完整握手 → CONNECT → 双向透传 → 连接日志链路。
/// </summary>
public class Socks5EndToEndTests : IAsyncLifetime
{
    private readonly FakeSshTunnelTransport _transport = new();
    private readonly CollectingSink _sink = new();
    private readonly TrafficCounter _traffic = new();
    private Socks5ProxyServer? _server;
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

    private async Task<Socks5ProxyServer> StartServerAsync(
        bool enableAuth = false,
        IProxyCredentialValidator? validator = null)
    {
        var server = new Socks5ProxyServer(_transport, new ProxyServerOptions
        {
            TunnelName = "集成测试隧道",
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
    public async Task Connect_PassthroughAuth_EchoRoundtrip_And_Logs()
    {
        _target = LocalTargetServer.StartEcho();
        var server = await StartServerAsync(enableAuth: false);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.BoundPort);
        using var stream = client.GetStream();

        // ① 握手（no-auth）
        await stream.WriteAsync(new byte[] { 5, 1, 0 });
        var handshakeReply = new byte[2];
        await ReadExactlyAsync(stream, handshakeReply);
        handshakeReply.Should().BeEquivalentTo(new byte[] { 5, 0 });

        // ② CONNECT 到目标回显服务器
        var payload = BuildIpv4ConnectRequest(_target.Port);
        await stream.WriteAsync(payload);
        var connectReply = new byte[10] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        await ReadExactlyAsync(stream, connectReply);
        connectReply[0].Should().Be(5);
        connectReply[1].Should().Be(Socks5Protocol.ReplySucceeded);

        // ③ 发送数据，验证回显
        var message = "hello-through-socks5-隧道";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(message));
        var echo = new byte[Encoding.UTF8.GetByteCount(message)];
        await ReadExactlyAsync(stream, echo);
        Encoding.UTF8.GetString(echo).Should().Be(message);

        // 关闭客户端以触发服务端记录连接日志（日志在连接释放时写入）。
        client.Dispose();

        // ④ 等连接日志落库断言
        var log = await _sink.WaitForAsync(l => l.TargetEndpoint.StartsWith("127.0.0.1:"));
        log.Status.Should().Be("Success");
        log.ProxyType.Should().Be(ProxyType.Socks5);
        log.TunnelName.Should().Be("集成测试隧道");
        log.TargetEndpoint.Should().Be($"127.0.0.1:{_target.Port}");
        log.BytesSent.Should().BeGreaterThan(0);
        log.BytesReceived.Should().BeGreaterThan(0);

        // ⑤ 流量计数也应累计
        _traffic.TotalBytesSent.Should().BeGreaterThan(0);
        _traffic.TotalBytesReceived.Should().BeGreaterThan(0);
        _traffic.TotalConnections.Should().Be(1);
    }

    [Fact]
    public async Task Connect_WithAuth_WrongCredential_Rejected()
    {
        _target = LocalTargetServer.StartEcho();
        var server = await StartServerAsync(
            enableAuth: true,
            validator: new FixedCredentialValidator { ExpectedUser = "alice", ExpectedPassword = "secret" });

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.BoundPort);
        using var stream = client.GetStream();

        // ① 握手（user/pass）
        await stream.WriteAsync(new byte[] { 5, 1, 2 });
        var handshakeReply = new byte[2];
        await ReadExactlyAsync(stream, handshakeReply);
        handshakeReply.Should().BeEquivalentTo(new byte[] { 5, 2 });

        // ② 认证失败
        await stream.WriteAsync(BuildAuthRequest("alice", "wrong"));
        var authReply = new byte[2];
        await ReadExactlyAsync(stream, authReply);
        authReply.Should().BeEquivalentTo(new byte[] { 1, 1 }); // 认证失败

        var log = await _sink.WaitForAsync(l => l.Status == "Failed");
        log.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Connect_WithAuth_CorrectCredential_Success()
    {
        _target = LocalTargetServer.StartEcho();
        var server = await StartServerAsync(
            enableAuth: true,
            validator: new FixedCredentialValidator { ExpectedUser = "alice", ExpectedPassword = "secret" });

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.BoundPort);
        using var stream = client.GetStream();

        await stream.WriteAsync(new byte[] { 5, 1, 2 });
        var handshakeReply = new byte[2];
        await ReadExactlyAsync(stream, handshakeReply);
        handshakeReply.Should().BeEquivalentTo(new byte[] { 5, 2 });

        await stream.WriteAsync(BuildAuthRequest("alice", "secret"));
        var authReply = new byte[2];
        await ReadExactlyAsync(stream, authReply);
        authReply.Should().BeEquivalentTo(new byte[] { 1, 0 }); // 认证成功

        await stream.WriteAsync(BuildIpv4ConnectRequest(_target.Port));
        var connectReply = new byte[10];
        await ReadExactlyAsync(stream, connectReply);
        connectReply[1].Should().Be(Socks5Protocol.ReplySucceeded);

        // 关闭客户端以触发服务端记录连接日志。
        client.Dispose();

        var log = await _sink.WaitForAsync(l => l.TargetEndpoint == $"127.0.0.1:{_target.Port}");
        log.Status.Should().Be("Success");
    }

    // ---- 辅助 ----

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

    /// <summary>构造 SOCKS5 CONNECT 请求（IPv4），目标为回环地址。</summary>
    private static byte[] BuildIpv4ConnectRequest(int port)
    {
        var payload = new byte[4 + 4 + 2];
        payload[0] = 5;
        payload[1] = Socks5Protocol.CommandConnect;
        payload[2] = 0;
        payload[3] = Socks5Protocol.AtypIpv4;
        payload[4] = 127;
        payload[5] = 0;
        payload[6] = 0;
        payload[7] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(8, 2), (ushort)port);
        return payload;
    }

    /// <summary>构造 SOCKS5 USERNAME/PASSWORD 认证请求（RFC 1929）。</summary>
    private static byte[] BuildAuthRequest(string user, string password)
    {
        var ub = Encoding.UTF8.GetBytes(user);
        var pb = Encoding.UTF8.GetBytes(password);
        var data = new byte[1 + 1 + ub.Length + 1 + pb.Length];
        data[0] = 1;
        data[1] = (byte)ub.Length;
        ub.CopyTo(data, 2);
        data[2 + ub.Length] = (byte)pb.Length;
        pb.CopyTo(data, 3 + ub.Length);
        return data;
    }
}
