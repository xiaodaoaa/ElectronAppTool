namespace NtpTool.Core.Ntp;

/// <summary>NTP 报文模式（Mode 字段）。</summary>
public enum NtpMode : byte
{
    Reserved = 0,
    SymmetricActive = 1,
    SymmetricPassive = 2,
    Client = 3,
    Server = 4,
    Broadcast = 5,
    Reserved6 = 6,
    Reserved7 = 7
}

/// <summary>
/// 表示一个标准 NTP 报文（固定 48 字节）。字段定义见需求文档第 6.2 节。
/// </summary>
public sealed class NtpPacket
{
    public byte LeapIndicator { get; set; }

    public byte VersionNumber { get; set; } = 4;

    public byte Mode { get; set; }

    public byte Stratum { get; set; }

    public byte Poll { get; set; }

    public byte Precision { get; set; }

    public uint RootDelay { get; set; }

    public uint RootDispersion { get; set; }

    public uint ReferenceId { get; set; }

    public DateTime ReferenceTimestamp { get; set; }

    public DateTime OriginateTimestamp { get; set; }

    public DateTime ReceiveTimestamp { get; set; }

    public DateTime TransmitTimestamp { get; set; }

    /// <summary>
    /// 解码时保留的原始 64 位时间戳位模式（大端 ulong），用于在服务端回显
    /// Originate Timestamp 等字段时做到逐位一致，避免 DateTime 往返导致的精度丢失。
    /// </summary>
    public ulong RawReferenceTimestamp { get; set; }

    public ulong RawOriginateTimestamp { get; set; }

    public ulong RawReceiveTimestamp { get; set; }

    public ulong RawTransmitTimestamp { get; set; }

    /// <summary>构造一个跳变指示符为 0、版本为 4 的空报文。</summary>
    public static NtpPacket CreateEmpty()
    {
        return new NtpPacket
        {
            LeapIndicator = 0,
            VersionNumber = 4,
            Mode = (byte)NtpMode.Reserved
        };
    }

    /// <summary>构造一个客户端请求报文（Mode = 3，其余字段按文档第 6.5 节填写）。</summary>
    public static NtpPacket CreateClientRequest(DateTime transmitUtc, byte poll = 6)
    {
        return new NtpPacket
        {
            LeapIndicator = 0,
            VersionNumber = 4,
            Mode = (byte)NtpMode.Client,
            Stratum = 0,
            Poll = poll,
            Precision = 0,
            RootDelay = 0,
            RootDispersion = 0,
            ReferenceId = 0,
            ReferenceTimestamp = NtpTime.Zero(),
            OriginateTimestamp = NtpTime.Zero(),
            ReceiveTimestamp = NtpTime.Zero(),
            TransmitTimestamp = transmitUtc
        };
    }
}