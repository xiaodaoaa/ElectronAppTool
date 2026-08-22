using FluentAssertions;
using SSHTunnelProxy.Core.Utils;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

public class AccentColorProviderTests
{
    [Fact]
    public void GetAccentArgb_WhenRegistryReturnsValue_UsesIt()
    {
        // 0xFF0078D4 = Win11 默认蓝
        var provider = new AccentColorProvider(() => 0xFF0078D4);

        provider.GetAccentArgb().Should().Be(0xFF0078D4);
    }

    [Fact]
    public void GetAccentArgb_WhenReaderReturnsNull_FallsBackToDefault()
    {
        var provider = new AccentColorProvider(() => null);

        provider.GetAccentArgb().Should().Be(AccentColorProvider.DefaultAccentArgb);
    }

    [Fact]
    public void GetAccentArgb_WhenReaderReturnsIntMinusOne_FallsBackToDefault()
    {
        // DWM"跟随系统默认"存 0xFFFFFFFF(-1)
        var provider = new AccentColorProvider(() => unchecked((uint)-1));

        provider.GetAccentArgb().Should().Be(AccentColorProvider.DefaultAccentArgb);
    }

    [Fact]
    public void GetAccentArgb_WhenMarkedDefault_IsWin11Blue()
    {
        AccentColorProvider.DefaultAccentArgb.Should().Be(0xFF0078D4);
    }
}
