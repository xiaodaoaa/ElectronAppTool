using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// SOCKS5 协议常量与帧解析工具（RFC 1928 / RFC 1929）。
/// 仅保留首期所需：CONNECT + 可选 USERNAME/PASSWORD 认证。
/// </summary>
public static class Socks5Protocol
{
    // VER
    public const byte Version = 0x05;

    // METHODS
    public const byte MethodNoAuth = 0x00;
    public const byte MethodUsernamePassword = 0x02;
    public const byte MethodNoAcceptable = 0xFF;

    // COMMANDS
    public const byte CommandConnect = 0x01;
    public const byte CommandBind = 0x02;
    public const byte CommandUdpAssociate = 0x03;

    // ATYP
    public const byte AtypIpv4 = 0x01;
    public const byte AtypDomain = 0x03;
    public const byte AtypIpv6 = 0x04;

    // REP
    public const byte ReplySucceeded = 0x00;
    public const byte ReplyGeneralFailure = 0x01;
    public const byte ReplyConnectionNotAllowed = 0x02;
    public const byte ReplyNetworkUnreachable = 0x03;
    public const byte ReplyHostUnreachable = 0x04;
    public const byte ReplyConnectionRefused = 0x05;
    public const byte ReplyTtlExpired = 0x06;
    public const byte ReplyCommandNotSupported = 0x07;
    public const byte ReplyAddressTypeNotSupported = 0x08;

    /// <summary>认证协商阶段的版本号（RFC 1929）。</summary>
    public const byte AuthVersion = 0x01;
    public const byte AuthSuccess = 0x00;
    public const byte AuthFailure = 0x01;

    /// <summary>解析握手阶段数据，返回客户端支持的方法列表；数据不足时返回 null。</summary>
    public static List<byte>? ParseHandshake(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 2)
            return null;

        if (buffer[0] != Version)
            throw new SocksProtocolException("不支持的 SOCKS 版本。");

        var nMethods = buffer[1];
        if (buffer.Length < 2 + nMethods)
            return null;

        var methods = new List<byte>(nMethods);
        for (var i = 0; i < nMethods; i++)
            methods.Add(buffer[2 + i]);
        return methods;
    }

    /// <summary>构造握手响应帧。</summary>
    public static byte[] BuildHandshakeReply(byte method)
        => [Version, method];

    /// <summary>解析认证请求（USERNAME/PASSWORD），返回用户名与密码；数据不足时返回 null。</summary>
    public static (string Username, string Password)? ParseAuthRequest(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 2)
            return null;

        if (buffer[0] != AuthVersion)
            throw new SocksProtocolException("不支持的认证版本。");

        var ulen = buffer[1];
        if (buffer.Length < 2 + ulen + 1)
            return null;

        var plen = buffer[2 + ulen];
        if (buffer.Length < 2 + ulen + 1 + plen)
            return null;

        var username = Encoding.UTF8.GetString(buffer.Slice(2, ulen));
        var password = Encoding.UTF8.GetString(buffer.Slice(3 + ulen, plen));
        return (username, password);
    }

    /// <summary>构造认证响应帧。</summary>
    public static byte[] BuildAuthReply(bool success)
        => [AuthVersion, success ? AuthSuccess : AuthFailure];

    /// <summary>解析 CONNECT 请求，返回目标主机与端口；数据不足时返回 null。</summary>
    public static (string Host, int Port)? ParseConnectRequest(ReadOnlySpan<byte> buffer, out byte command)
    {
        command = 0;
        if (buffer.Length < 4)
            return null;

        if (buffer[0] != Version)
            throw new SocksProtocolException("不支持的 SOCKS 版本。");

        command = buffer[1];
        // byte 2 = RSV (0x00)，可忽略。
        var atyp = buffer[3];

        switch (atyp)
        {
            case AtypIpv4:
                if (buffer.Length < 4 + 4 + 2)
                    return null;
                var ipv4 = new IPAddress(buffer.Slice(4, 4).ToArray());
                var port4 = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(8, 2));
                return (ipv4.ToString(), port4);

            case AtypDomain:
                var len = buffer[4];
                if (buffer.Length < 5 + len + 2)
                    return null;
                var host = Encoding.UTF8.GetString(buffer.Slice(5, len));
                var portD = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(5 + len, 2));
                return (host, portD);

            case AtypIpv6:
                if (buffer.Length < 4 + 16 + 2)
                    return null;
                var ipv6 = new IPAddress(buffer.Slice(4, 16).ToArray());
                var port6 = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(20, 2));
                return (ipv6.ToString(), port6);

            default:
                throw new SocksProtocolException($"不支持的地址类型：0x{atyp:X2}");
        }
    }

    /// <summary>
    /// 构造 CONNECT 响应帧（携带绑定地址信息）。
    /// </summary>
    public static byte[] BuildConnectReply(byte rep, IPAddress? bindAddress = null, int bindPort = 0)
    {
        bindAddress ??= IPAddress.Loopback;
        var atyp = bindAddress.AddressFamily == AddressFamily.InterNetworkV6 ? AtypIpv6 : AtypIpv4;
        var addrBytes = atyp == AtypIpv6 ? bindAddress.GetAddressBytes() : bindAddress.MapToIPv4().GetAddressBytes();

        var reply = new byte[4 + addrBytes.Length + 2];
        reply[0] = Version;
        reply[1] = rep;
        reply[2] = 0x00; // RSV
        reply[3] = atyp;
        addrBytes.CopyTo(reply, 4);
        BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(4 + addrBytes.Length, 2), (ushort)bindPort);
        return reply;
    }

    /// <summary>将异常映射为 SOCKS5 应答码。</summary>
    public static byte MapErrorToReply(Exception ex)
    {
        return ex switch
        {
            SocksProtocolException => ReplyGeneralFailure,
            SocketException { SocketErrorCode: SocketError.ConnectionRefused } => ReplyConnectionRefused,
            SocketException { SocketErrorCode: SocketError.HostUnreachable } => ReplyHostUnreachable,
            SocketException { SocketErrorCode: SocketError.NetworkUnreachable } => ReplyNetworkUnreachable,
            OperationCanceledException or TimeoutException => ReplyTtlExpired,
            _ => ReplyGeneralFailure,
        };
    }
}

/// <summary>SOCKS5 协议解析异常。</summary>
public sealed class SocksProtocolException : Exception
{
    public SocksProtocolException(string message) : base(message)
    {
    }
}
