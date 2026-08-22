using FluentAssertions;
using SSHTunnelProxy.Core.Proxy;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

public class Socks5ProtocolTests
{
    [Fact]
    public void ParseHandshake_ValidInput_ReturnsMethods()
    {
        byte[] data = [0x05, 0x02, 0x00, 0x02]; // VER=5, 2 methods: noauth, userpass

        var methods = Socks5Protocol.ParseHandshake(data);

        methods.Should().BeEquivalentTo(new byte[] { 0x00, 0x02 });
    }

    [Fact]
    public void ParseHandshake_IncompleteData_ReturnsNull()
    {
        byte[] data = [0x05, 0x03, 0x00]; // 声明 3 个方法但只提供 1 个

        Socks5Protocol.ParseHandshake(data).Should().BeNull();
    }

    [Fact]
    public void ParseHandshake_WrongVersion_Throws()
    {
        byte[] data = [0x04, 0x00];

        var act = () => Socks5Protocol.ParseHandshake(data);

        act.Should().Throw<SocksProtocolException>();
    }

    [Theory]
    [InlineData(0x01, new byte[] { 192, 168, 1, 1 }, 1080)]
    public void ParseConnectRequest_Ipv4_ReturnsExpected(byte atyp, byte[] targetBytes, int port)
    {
        // 构造请求：VER=5, CMD=1, RSV=0, ATYP, 地址, 端口
        var req = new List<byte> { 0x05, 0x01, 0x00, atyp };
        req.AddRange(targetBytes);
        req.Add((byte)(port >> 8));
        req.Add((byte)(port & 0xFF));

        var parsed = Socks5Protocol.ParseConnectRequest(req.ToArray(), out var command);

        parsed.Should().NotBeNull();
        command.Should().Be(Socks5Protocol.CommandConnect);
        parsed!.Value.Port.Should().Be(port);
    }

    [Fact]
    public void ParseConnectRequest_Domain_ReturnsHostName()
    {
        // CONNECT to "www.example.com:80"
        byte[] req =
        [
            0x05, 0x01, 0x00, 0x03, // VER CMD RSV ATYP(domain)
            15, (byte)'w', (byte)'w', (byte)'w', (byte)'.', // len + "www."
            (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e', (byte)'.', // example.
            (byte)'c', (byte)'o', (byte)'m', // com
            0x00, 0x50, // port 80
        ];

        var parsed = Socks5Protocol.ParseConnectRequest(req, out _);

        parsed.Should().NotBeNull();
        parsed!.Value.Host.Should().Be("www.example.com");
        parsed.Value.Port.Should().Be(80);
    }

    [Fact]
    public void BuildConnectReply_Succeeded_HasCorrectByteSequence()
    {
        var reply = Socks5Protocol.BuildConnectReply(Socks5Protocol.ReplySucceeded, System.Net.IPAddress.Loopback, 0);

        reply[0].Should().Be(0x05);
        reply[1].Should().Be(Socks5Protocol.ReplySucceeded);
        reply[2].Should().Be(0);
        reply[3].Should().Be(Socks5Protocol.AtypIpv4);
    }

    [Fact]
    public void BuildAuthReply_Success_ReturnsZero()
    {
        var reply = Socks5Protocol.BuildAuthReply(true);
        reply.Should().BeEquivalentTo(new byte[] { 0x01, 0x00 });
    }

    [Fact]
    public void MapErrorToReply_ConnectionRefused_ReturnsProperCode()
    {
        var ex = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused);
        Socks5Protocol.MapErrorToReply(ex).Should().Be(Socks5Protocol.ReplyConnectionRefused);
    }
}
