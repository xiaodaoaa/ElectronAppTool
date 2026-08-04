using System.Buffers.Binary;

namespace NtpTool.Core.Ntp;

/// <summary>
/// NTP 报文（48 字节）的序列化与反序列化。字节序为大端。
/// 结构见需求文档第 6.1 节。
/// </summary>
public static class NtpPacketCodec
{
    public const int PacketSize = 48;

    /// <summary>将一个报文序列化为 48 字节数组。</summary>
    public static byte[] Encode(NtpPacket packet)
    {
        var buffer = new byte[PacketSize];

        byte first = (byte)((packet.LeapIndicator & 0x03) << 6
                            | (packet.VersionNumber & 0x07) << 3
                            | (packet.Mode & 0x07));
        buffer[0] = first;
        buffer[1] = packet.Stratum;
        buffer[2] = packet.Poll;
        buffer[3] = packet.Precision;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), packet.RootDelay);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8, 4), packet.RootDispersion);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(12, 4), packet.ReferenceId);

        WriteTimestampRaw(buffer.AsSpan(16, 8), packet.RawReferenceTimestamp, packet.ReferenceTimestamp);
        WriteTimestampRaw(buffer.AsSpan(24, 8), packet.RawOriginateTimestamp, packet.OriginateTimestamp);
        WriteTimestampRaw(buffer.AsSpan(32, 8), packet.RawReceiveTimestamp, packet.ReceiveTimestamp);
        WriteTimestampRaw(buffer.AsSpan(40, 8), packet.RawTransmitTimestamp, packet.TransmitTimestamp);

        return buffer;
    }

    /// <summary>
    /// 写入时间戳：优先使用原始 64 位位模式（用于服务端逐位回显 Originate 等），
    /// 未设置时回退到 DateTime 转换。
    /// </summary>
    private static void WriteTimestampRaw(Span<byte> target, ulong raw, DateTime utc)
    {
        ulong value = raw;
        if (value == 0)
        {
            value = NtpTime.ToFixed64(utc);
        }

        BinaryPrimitives.WriteUInt64BigEndian(target, value);
    }

    /// <summary>
    /// 从字节数组解析报文。长度必须等于 <see cref="PacketSize"/>，否则抛出 <see cref="NtpPacketException"/>。
    /// </summary>
    public static NtpPacket Decode(byte[] buffer)
    {
        if (buffer.Length != PacketSize)
        {
            throw new NtpPacketException($"NTP 报文长度无效：{buffer.Length}，应为 {PacketSize}。");
        }

        var packet = new NtpPacket();
        byte first = buffer[0];
        packet.LeapIndicator = (byte)((first >> 6) & 0x03);
        packet.VersionNumber = (byte)((first >> 3) & 0x07);
        packet.Mode = (byte)(first & 0x07);

        packet.Stratum = buffer[1];
        packet.Poll = buffer[2];
        packet.Precision = (sbyte)buffer[3] < 0 ? (byte)(buffer[3] & 0xFF) : buffer[3];

        packet.RootDelay = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4, 4));
        packet.RootDispersion = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(8, 4));
        packet.ReferenceId = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(12, 4));

        packet.RawReferenceTimestamp = ReadRawTimestamp(buffer.AsSpan(16, 8));
        packet.RawOriginateTimestamp = ReadRawTimestamp(buffer.AsSpan(24, 8));
        packet.RawReceiveTimestamp = ReadRawTimestamp(buffer.AsSpan(32, 8));
        packet.RawTransmitTimestamp = ReadRawTimestamp(buffer.AsSpan(40, 8));

        packet.ReferenceTimestamp = NtpTime.FromFixed64(packet.RawReferenceTimestamp);
        packet.OriginateTimestamp = NtpTime.FromFixed64(packet.RawOriginateTimestamp);
        packet.ReceiveTimestamp = NtpTime.FromFixed64(packet.RawReceiveTimestamp);
        packet.TransmitTimestamp = NtpTime.FromFixed64(packet.RawTransmitTimestamp);

        return packet;
    }

    /// <summary>将 8 字节的时间戳写入 spans（大端）。</summary>
    /// <summary>
    /// 读取 8 字节的原始 64 位时间戳（大端），不做 DateTime 转换，用于逐位回显。
    /// </summary>
    private static ulong ReadRawTimestamp(ReadOnlySpan<byte> source)
    {
        return BinaryPrimitives.ReadUInt64BigEndian(source);
    }
}

/// <summary>表示 NTP 报文格式错误。</summary>
public sealed class NtpPacketException : Exception
{
    public NtpPacketException(string message) : base(message) { }
    public NtpPacketException(string message, Exception inner) : base(message, inner) { }
}