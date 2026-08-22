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
}
