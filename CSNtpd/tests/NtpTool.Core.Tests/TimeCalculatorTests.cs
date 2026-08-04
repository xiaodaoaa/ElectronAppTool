using NtpTool.Core.Ntp;

namespace NtpTool.Core.Tests;

/// <summary>
/// Offset / Delay 计算测试，使用需求文档第 16.1 节的示例数据：
///   T1 = 10:00:00.000
///   T2 = 10:00:00.100
///   T3 = 10:00:00.110
///   T4 = 10:00:00.030
///   期望 Offset = 90ms，Delay = 20ms
/// </summary>
public class TimeCalculatorTests
{
    private static DateTime T(int second, int ms) => new(2024, 6, 22, 10, 0, second, ms, DateTimeKind.Utc);

    [Fact]
    public void Offset_And_Delay_Match_Documented_Example()
    {
        DateTime t1 = T(0, 0);
        DateTime t2 = T(0, 100);
        DateTime t3 = T(0, 110);
        DateTime t4 = T(0, 30);

        TimeSample sample = TimeCalculator.Calculate(t1, t2, t3, t4);

        Assert.Equal(90.0, sample.OffsetMs, 3);
        Assert.Equal(20.0, sample.RoundTripDelayMs, 3);
    }

    [Fact]
    public void IsValid_Is_False_For_Negative_Delay()
    {
        // 构造一个往返延迟为负的异常样本（(T4-T1) < (T3-T2)）
        DateTime t1 = T(0, 0);
        DateTime t2 = T(0, 0);
        DateTime t3 = T(1, 0);
        DateTime t4 = T(0, 10);
        TimeSample sample = TimeCalculator.Calculate(t1, t2, t3, t4);
        Assert.True(sample.RoundTripDelayMs < 0);
        Assert.False(sample.IsValid(10_000));
    }

    [Fact]
    public void IsValid_Is_False_For_Oversized_Delay()
    {
        TimeSample sample = TimeCalculator.Calculate(T(0, 0), T(0, 1), T(0, 2), T(15, 0));
        Assert.True(sample.RoundTripDelayMs > 10_000);
        Assert.False(sample.IsValid(10_000));
    }

    [Fact]
    public void IsValid_Is_True_For_Reasonable_Delay()
    {
        TimeSample sample = TimeCalculator.Calculate(T(0, 0), T(0, 100), T(0, 110), T(0, 30));
        Assert.True(sample.IsValid(10_000));
    }

    [Fact]
    public void ZeroOffset_When_Timestamps_Symmetric()
    {
        DateTime t1 = T(0, 0);
        DateTime t2 = T(0, 0);
        DateTime t3 = T(0, 0);
        DateTime t4 = T(0, 0);
        TimeSample sample = TimeCalculator.Calculate(t1, t2, t3, t4);
        Assert.Equal(0.0, sample.OffsetMs, 3);
        Assert.Equal(0.0, sample.RoundTripDelayMs, 3);
    }

    [Fact]
    public void CalculateFromResponse_Uses_Server_Timestamps()
    {
        DateTime t1 = T(0, 0);
        DateTime t4 = T(0, 30);
        var response = new NtpPacket
        {
            ReceiveTimestamp = T(0, 100),
            TransmitTimestamp = T(0, 110)
        };
        TimeSample sample = TimeCalculator.CalculateFromResponse(response, t1, t4);
        Assert.Equal(90.0, sample.OffsetMs, 3);
        Assert.Equal(20.0, sample.RoundTripDelayMs, 3);
    }
}