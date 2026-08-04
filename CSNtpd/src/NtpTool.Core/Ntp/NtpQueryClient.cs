using System.Net;
using System.Net.Sockets;

namespace NtpTool.Core.Ntp;

/// <summary>单个上游服务器同步失败时抛出的异常。</summary>
public sealed class NtpExchangeException : Exception
{
    public string Server { get; }
    public NtpExchangeException(string server, string message) : base($"{server}: {message}")
    {
        Server = server;
    }
    public NtpExchangeException(string server, string message, Exception inner) : base($"{server}: {message}", inner)
    {
        Server = server;
    }
}

/// <summary>
/// 一次 NTP 交换的原始结果：响应报文与四组时间戳。
/// </summary>
public sealed class NtpExchange
{
    public NtpPacket Response { get; init; } = NtpPacket.CreateEmpty();
    public DateTime T1 { get; init; }
    public DateTime T4 { get; init; }
}

/// <summary>
/// 使用 <see cref="System.Net.Sockets.UdpClient"/> 与单个 NTP 服务器进行一次同步。
/// 对应需求文档第 8.2 节流程。
/// </summary>
public sealed class NtpQueryClient
{
    /// <summary>
    /// 向指定服务器发送查询并等待响应。客户端请求报文见需求文档第 6.5 节。
    /// </summary>
    /// <exception cref="NtpExchangeException">网络、超时或报文非法时抛出。</exception>
    public async ValueTask<NtpExchange> QueryAsync(
        string host,
        int port,
        int timeoutMs,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        DateTime t1 = clock.GetUtcNow().UtcDateTime;
        DateTime targetTransmit = t1;

        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.ReceiveTimeout = timeoutMs;
        NtpPacket request = NtpPacket.CreateClientRequest(targetTransmit);
        byte[] requestBytes = NtpPacketCodec.Encode(request);

        IPEndPoint? remote = null;
        try
        {
            if (IPAddress.TryParse(host, out IPAddress? address))
            {
                remote = new IPEndPoint(address, port);
            }
            else
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                if (addresses.Length == 0 || addresses[0].AddressFamily != AddressFamily.InterNetwork)
                {
                    var v6 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (v6 is null)
                    {
                        throw new NtpExchangeException(host, "DNS 解析失败或没有 IPv4 地址。");
                    }
                    address = v6;
                    // 需要 IPv6 socket
                    using var v6Client = new UdpClient(AddressFamily.InterNetworkV6);
                    return await QueryAsyncV6(v6Client, address, port, t1, timeoutMs, cancellationToken).ConfigureAwait(false);
                }
                remote = new IPEndPoint(addresses[0], port);
            }
        }
        catch (NtpExchangeException)
        {
            throw;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
        {
            throw new NtpExchangeException(host, "DNS 解析失败。", ex);
        }

        try
        {
            await client.SendAsync(requestBytes, requestBytes.Length, remote).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new NtpExchangeException(host, "发送 UDP 请求失败。", ex);
        }

        try
        {
            var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            DateTime t4 = clock.GetUtcNow().UtcDateTime;
            NtpPacket response;
            try
            {
                response = NtpPacketCodec.Decode(result.Buffer);
            }
            catch (NtpPacketException ex)
            {
                throw new NtpExchangeException(host, $"响应报文无效：{ex.Message}", ex);
            }

            if (response.Mode != (byte)NtpMode.Server)
            {
                throw new NtpExchangeException(host, $"响应 Mode 错误：{response.Mode}，应为其服务端(4)。");
            }
            if (response.TransmitTimestamp == NtpTime.Zero())
            {
                throw new NtpExchangeException(host, "响应缺少 Transmit Timestamp。");
            }

            return new NtpExchange { Response = response, T1 = t1, T4 = t4 };
        }
        catch (NtpExchangeException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new NtpExchangeException(host, "请求超时（无响应）。");
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.TimedOut or SocketError.WouldBlock)
        {
            throw new NtpExchangeException(host, "请求超时（无响应）。", ex);
        }
        catch (Exception ex)
        {
            throw new NtpExchangeException(host, "接收响应失败。", ex);
        }
    }

    private static async ValueTask<NtpExchange> QueryAsyncV6(UdpClient client, IPAddress address, int port, DateTime t1, int timeoutMs, CancellationToken ct)
    {
        client.Client.ReceiveTimeout = timeoutMs;
        NtpPacket request = NtpPacket.CreateClientRequest(t1);
        byte[] requestBytes = NtpPacketCodec.Encode(request);
        IPEndPoint remote = new(address, port);
        await client.SendAsync(requestBytes, remote).ConfigureAwait(false);
        var result = await client.ReceiveAsync(ct).ConfigureAwait(false);
        DateTime t4 = TimeProvider.System.GetUtcNow().UtcDateTime;
        NtpPacket response = NtpPacketCodec.Decode(result.Buffer);
        if (response.Mode != (byte)NtpMode.Server)
        {
            throw new NtpExchangeException($"{address}", $"响应 Mode 错误：{response.Mode}。");
        }
        return new NtpExchange { Response = response, T1 = t1, T4 = t4 };
    }
}
