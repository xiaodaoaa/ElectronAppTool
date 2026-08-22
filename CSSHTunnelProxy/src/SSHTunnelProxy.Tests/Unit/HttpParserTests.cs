using FluentAssertions;
using SSHTunnelProxy.Core.Proxy;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

public class HttpParserTests
{
    [Fact]
    public void ParseHeaders_ConnectRequest_ReturnsTarget()
    {
        var data = "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\n\r\n"u8.ToArray();

        var req = HttpParser.ParseHeaders(data);

        req.Should().NotBeNull();
        req!.Value.Method.Should().Be("CONNECT");
        req.Value.ConnectTarget.Should().Be("example.com:443");

        var (host, port) = HttpParser.ParseAuthority(req.Value.ConnectTarget!);
        host.Should().Be("example.com");
        port.Should().Be(443);
    }

    [Fact]
    public void ParseHeaders_Incomplete_ReturnsNull()
    {
        var data = "CONNECT example.com:443 HTTP/1.1\r\nHost: exa"u8.ToArray();

        HttpParser.ParseHeaders(data).Should().BeNull();
    }

    [Fact]
    public void ParseHeaders_ExtractsHeaders()
    {
        var data = "GET / HTTP/1.1\r\nHost: a.com\r\nUser-Agent: test\r\n\r\n"u8.ToArray();

        var req = HttpParser.ParseHeaders(data);

        req!.Value.Headers["Host"].Should().Be("a.com");
        req.Value.Headers["User-Agent"].Should().Be("test");
    }

    [Theory]
    [InlineData("example.com:80", "example.com", 80)]
    [InlineData("[::1]:443", "::1", 443)]
    [InlineData("192.168.1.1:22", "192.168.1.1", 22)]
    public void ParseAuthority_ReturnsHostAndPort(string authority, string expectedHost, int expectedPort)
    {
        var (host, port) = HttpParser.ParseAuthority(authority);

        host.Should().Be(expectedHost);
        port.Should().Be(expectedPort);
    }

    [Fact]
    public void ParseAuthority_Invalid_Throws()
    {
        var act = () => HttpParser.ParseAuthority("no-port");

        act.Should().Throw<HttpParseException>();
    }

    [Fact]
    public void ParseHeaders_InvalidRequestLine_Throws()
    {
        var data = "GET\r\n\r\n"u8.ToArray();

        var act = () => HttpParser.ParseHeaders(data);

        act.Should().Throw<HttpParseException>();
    }
}
