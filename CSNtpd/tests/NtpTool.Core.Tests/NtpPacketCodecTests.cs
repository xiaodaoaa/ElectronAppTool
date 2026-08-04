using System.Buffers.Binary;
using NtpTool.Core.Ntp;

namespace NtpTool.Core.Tests;

public class NtpTimeTests
{
    [Fact]
    public void ToFixed64_And_FromFixed64_RoundTrip()
    {
        var original = new DateTime(2024, 6, 22, 10, 30, 15, 250, DateTimeKind.Utc);
        ulong raw = NtpTime.ToFixed64(original);
        DateTime back = NtpTime.FromFixed64(raw);
        double toleranceMs = 1.0;
        Assert.True(Math.Abs((back - original).TotalMilliseconds) < toleranceMs,
            $"往返误差过大：{back:M} 与 {original} 差 {(back - original).TotalMilliseconds}ms");
    }

    [Fact]
    public void NtpEpoch_Is_1900()
    {
        Assert.Equal(1900, NtpTime.NtpEpoch.Year);
    }

    [Fact]
    public void EpochDifference_Is_2208988800()
    {
        Assert.Equal(2_208_988_800L, NtpTime.EpochDifferenceSeconds);
    }

    [Fact]
    public void NtpEpoch_Converts_To_Zero()
    {
        var (seconds, fraction) = NtpTime.ToNtpTimestamp(NtpTime.NtpEpoch);
        Assert.Equal(0u, seconds);
        Assert.Equal(0u, fraction);
    }

    [Fact]
    public void UnixEpoch_Converts_To_EpochDifference()
    {
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var (seconds, _) = NtpTime.ToNtpTimestamp(unixEpoch);
        Assert.Equal(NtpTime.EpochDifferenceSeconds, seconds);
    }

    [Fact]
    public void LocalTime_Is_Converted_As_Utc()
    {
        // 构造一个本地时间对象，验证内部使用 ToUniversalTime 转换
        var local = new DateTime(2024, 6, 22, 10, 30, 0, DateTimeKind.Local);
        var utc = local.ToUniversalTime();
        var (seconds1, _) = NtpTime.ToNtpTimestamp(local);
        var (seconds2, _) = NtpTime.ToNtpTimestamp(utc);
        Assert.Equal(seconds2, seconds1);
    }
}

public class NtpPacketCodecTests
{
    [Fact]
    public void ClientRequest_Encodes_To_48_Bytes()
    {
        NtpPacket request = NtpPacket.CreateClientRequest(DateTime.UtcNow);
        byte[] buffer = NtpPacketCodec.Encode(request);
        Assert.Equal(48, buffer.Length);
    }

    [Fact]
    public void ClientRequest_Has_Mode3_Version4()
    {
        NtpPacket request = NtpPacket.CreateClientRequest(DateTime.UtcNow);
        byte[] buffer = NtpPacketCodec.Encode(request);

        byte first = buffer[0];
        Assert.Equal(0, (first >> 6) & 0x03); // LI
        Assert.Equal(4, (first >> 3) & 0x07); // VN
        Assert.Equal(3, first & 0x07);        // Mode
    }

    [Fact]
    public void ClientRequest_Writes_TransmitTimestamp()
    {
        var transmit = new DateTime(2024, 6, 22, 10, 30, 0, DateTimeKind.Utc);
        NtpPacket request = NtpPacket.CreateClientRequest(transmit);
        byte[] buffer = NtpPacketCodec.Encode(request);
        ulong raw = BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(40, 8));
        DateTime back = NtpTime.FromFixed64(raw);
        Assert.True(Math.Abs((back - transmit).TotalMilliseconds) < 1.0);
    }

    [Fact]
    public void Decode_Of_Encoded_Packet_Preserves_Fields()
    {
        var packet = new NtpPacket
        {
            LeapIndicator = 1,
            VersionNumber = 4,
            Mode = 4,
            Stratum = 2,
            Poll = 6,
            Precision = 6,
            RootDelay = 0x00100000,
            RootDispersion = 0x00001000,
            ReferenceId = 0x4C4F4341,
            ReferenceTimestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OriginateTimestamp = new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc),
            ReceiveTimestamp = new DateTime(2024, 1, 1, 0, 0, 2, DateTimeKind.Utc),
            TransmitTimestamp = new DateTime(2024, 1, 1, 0, 0, 3, DateTimeKind.Utc)
        };

        byte[] buffer = NtpPacketCodec.Encode(packet);
        NtpPacket decoded = NtpPacketCodec.Decode(buffer);

        Assert.Equal(packet.LeapIndicator, decoded.LeapIndicator);
        Assert.Equal(packet.VersionNumber, decoded.VersionNumber);
        Assert.Equal(packet.Mode, decoded.Mode);
        Assert.Equal(packet.Stratum, decoded.Stratum);
        Assert.Equal(packet.Poll, decoded.Poll);
        Assert.Equal(packet.RootDelay, decoded.RootDelay);
        Assert.Equal(packet.RootDispersion, decoded.RootDispersion);
        Assert.Equal(packet.ReferenceId, decoded.ReferenceId);
        Assert.Equal(packet.ReferenceTimestamp.Ticks, decoded.ReferenceTimestamp.Ticks);
        Assert.Equal(packet.OriginateTimestamp.Ticks, decoded.OriginateTimestamp.Ticks);
        Assert.Equal(packet.ReceiveTimestamp.Ticks, decoded.ReceiveTimestamp.Ticks);
        Assert.Equal(packet.TransmitTimestamp.Ticks, decoded.TransmitTimestamp.Ticks);
    }

    [Fact]
    public void Decode_Rejects_WrongLength()
    {
        Assert.Throws<NtpPacketException>(() => NtpPacketCodec.Decode(new byte[47]));
        Assert.Throws<NtpPacketException>(() => NtpPacketCodec.Decode(new byte[49]));
    }

    [Fact]
    public void ZeroPacket_AllFieldsAreZero()
    {
        NtpPacket request = NtpPacket.CreateClientRequest(DateTime.UtcNow);
        Assert.Equal(0, request.LeapIndicator);
        Assert.Equal(0, request.Stratum);
        Assert.Equal(0u, request.RootDelay);
        Assert.Equal(0u, request.RootDispersion);
    }

    [Fact]
    public void Raw_TransmitTimestamp_Is_Bitwise_Preserved_ThroughEncodeDecode()
    {
        // 关键回归测试：模拟 ntpdate 场景，客户端请求的 Transmit 时间戳
        // 经 编码→服务端解码→回应 必须逐位一致（否则 ntpdate 报 pkt.org != peer.xmt）。
        var clientTransmit = DateTime.UtcNow; // 当前时间，含纳秒精度
        NtpPacket request = NtpPacket.CreateClientRequest(clientTransmit);
        byte[] requestBytes = NtpPacketCodec.Encode(request);

        // 服务端解码
        NtpPacket decodedRequest = NtpPacketCodec.Decode(requestBytes);

        // 服务端构造响应：原样回显请求者的 Transmit 原始位
        var response = new NtpPacket
        {
            RawOriginateTimestamp = decodedRequest.RawTransmitTimestamp,
            OriginateTimestamp = decodedRequest.TransmitTimestamp
        };
        byte[] responseBytes = NtpPacketCodec.Encode(response);

        // 客户端校验：响应 Origin(24) 是否与请求 Transmit(40) 逐位一致
        ulong echoed = BinaryPrimitives.ReadUInt64BigEndian(responseBytes.AsSpan(24, 8));
        ulong sent = BinaryPrimitives.ReadUInt64BigEndian(requestBytes.AsSpan(40, 8));
        Assert.Equal(sent, echoed);
    }

    [Fact]
    public void DecodedPacket_Captures_RawTimestamp_Bits()
    {
        // 构造一个无法被 DateTime 精确表示的 64 位时间戳（极端小数位）
        byte[] buffer = new byte[48];
        buffer[0] = 0x24;
        buffer[1] = 1;
// 手动写入一个随机高精度 Origin 时间戳
        ulong raw = 0xE7_6F_A0_00_00_00_00_01ul; // 具象化的任意位模式（含低 32 位小数极端值）
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(24, 8), raw);

        NtpPacket decoded = NtpPacketCodec.Decode(buffer);
        Assert.Equal(raw, decoded.RawOriginateTimestamp);
    }
}