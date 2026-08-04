namespace NtpTool.Core.Ntp;

/// <summary>一次时间采样计算出的结果。</summary>
public readonly struct TimeSample
{
    public DateTime T1 { get; }
    public DateTime T2 { get; }
    public DateTime T3 { get; }
    public DateTime T4 { get; }

    /// <summary>本地时钟与远端时间偏差，毫秒。</summary>
    public double OffsetMs { get; }

    /// <summary>网络往返延迟，毫秒。</summary>
    public double RoundTripDelayMs { get; }

    public TimeSample(DateTime t1, DateTime t2, DateTime t3, DateTime t4)
    {
        T1 = t1;
        T2 = t2;
        T3 = t3;
        T4 = t4;
        OffsetMs = ((T2 - T1).TotalMilliseconds + (T3 - T4).TotalMilliseconds) / 2.0;
        RoundTripDelayMs = (T4 - T1).TotalMilliseconds - (T3 - T2).TotalMilliseconds;
    }

    /// <summary>往返延迟是否为合理的正值（负值或过小说明时间不可信）。</summary>
    public bool IsValid(double maxAcceptableDelayMs)
    {
        return RoundTripDelayMs >= 0 && RoundTripDelayMs <= maxAcceptableDelayMs;
    }
}

/// <summary>
/// 根据四个时间戳计算 Offset 与 Round Trip Delay。公式见需求文档第 6.7 节：
///   Offset = ((T2 - T1) + (T3 - T4)) / 2
///   RoundTripDelay = (T4 - T1) - (T3 - T2)
/// </summary>
public static class TimeCalculator
{
    public const double DefaultMaxAcceptableDelayMs = 10_000;

    public static TimeSample Calculate(
        DateTime t1,
        DateTime t2,
        DateTime t3,
        DateTime t4,
        double maxAcceptableDelayMs = DefaultMaxAcceptableDelayMs)
    {
        return new TimeSample(t1, t2, t3, t4);
    }

    /// <summary>
    /// 根据客户端发送时间与响应时间戳计算偏移。T4 为本地接收时间（由调用方记录）。
    /// </summary>
    public static TimeSample CalculateFromResponse(NtpPacket response, DateTime t1, DateTime t4, double maxAcceptableDelayMs = DefaultMaxAcceptableDelayMs)
    {
        return new TimeSample(t1, response.ReceiveTimestamp, response.TransmitTimestamp, t4);
    }
}