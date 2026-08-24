using FluentAssertions;
using SSHTunnelProxy.Core.Tunnel;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

public class TrafficCounterTests
{
    [Fact]
    public void AddSentReceived_AccumulatesTotals()
    {
        var counter = new TrafficCounter();

        counter.AddSent(100);
        counter.AddSent(200);
        counter.AddReceived(50);

        counter.TotalBytesSent.Should().Be(300);
        counter.TotalBytesReceived.Should().Be(50);
    }

    [Fact]
    public void AddConnection_RemoveConnection_TracksActive()
    {
        var counter = new TrafficCounter();

        counter.AddConnection();
        counter.AddConnection();
        counter.ActiveConnections.Should().Be(2);

        counter.RemoveConnection();
        counter.ActiveConnections.Should().Be(1);
        counter.TotalConnections.Should().Be(2);
    }

    [Fact]
    public void Sample_ReturnsReasonableRates()
    {
        var counter = new TrafficCounter();
        counter.AddSent(1_000_000);

        var (up, down) = counter.Sample();

        up.Should().BeGreaterThan(0);
        down.Should().Be(0);
    }

    [Fact]
    public void Sample_SteadyState_ReturnsAccurateRate()
    {
        var counter = new TrafficCounter();

        // 模拟稳态：每个采样周期固定发送 1000 字节、采样一次。
        // 窗口填满（SampleCount 个周期）后，速率应精确反映 1000 字节/周期，
        // 而非被滑动窗口的清零时序系统性低估（原实现稳态返回 800）。
        for (var i = 0; i < 10; i++)
        {
            counter.AddSent(1000);
            counter.Sample();
        }

        counter.AddSent(1000);
        var (up, _) = counter.Sample();

        up.Should().Be(1000);
    }
}
