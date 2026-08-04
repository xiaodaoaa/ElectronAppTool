using System.Net;
using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.Core.Tests;

public class NetworkAccessControllerTests
{
    [Fact]
    public void AllowAll_Accepts_Any_Client()
    {
        var options = new NtpServerOptions { AllowAllClients = true };
        using var controller = new NetworkAccessController(options);
        Assert.True(controller.IsAllowed(IPAddress.Parse("192.168.99.99")));
    }

    [Fact]
    public void Whitelist_Accepts_Matching_Ip()
    {
        var options = new NtpServerOptions { AllowAllClients = false };
        options.AllowedNetworks.Add("192.168.1.10");
        using var controller = new NetworkAccessController(options);
        Assert.True(controller.IsAllowed(IPAddress.Parse("192.168.1.10")));
    }

    [Fact]
    public void Whitelist_Rejects_NonMatching_Ip()
    {
        var options = new NtpServerOptions { AllowAllClients = false };
        options.AllowedNetworks.Add("192.168.1.10");
        using var controller = new NetworkAccessController(options);
        Assert.False(controller.IsAllowed(IPAddress.Parse("10.0.0.5")));
    }

    [Fact]
    public void Cidr_Matches_Within_Subnet()
    {
        var options = new NtpServerOptions { AllowAllClients = false };
        options.AllowedNetworks.Add("192.168.1.0/24");
        using var controller = new NetworkAccessController(options);
        Assert.True(controller.IsAllowed(IPAddress.Parse("192.168.1.200")));
        Assert.False(controller.IsAllowed(IPAddress.Parse("192.168.2.1")));
    }

    [Fact]
    public void RateLimit_Exceeds_After_Limit()
    {
        var options = new NtpServerOptions { RateLimitPerMinute = 3 };
        using var controller = new NetworkAccessController(options);
        var ip = IPAddress.Parse("10.0.0.1");
        Assert.False(controller.IsRateLimited(ip));
        Assert.False(controller.IsRateLimited(ip));
        Assert.False(controller.IsRateLimited(ip));
        Assert.True(controller.IsRateLimited(ip)); // 第 4 次触发
    }

    [Fact]
    public void RateLimit_Is_Per_Ip()
    {
        var options = new NtpServerOptions { RateLimitPerMinute = 2 };
        using var controller = new NetworkAccessController(options);
        var ipA = IPAddress.Parse("10.0.0.1");
        var ipB = IPAddress.Parse("10.0.0.2");
        Assert.False(controller.IsRateLimited(ipA)); // 第 1 次
        Assert.False(controller.IsRateLimited(ipA)); // 第 2 次
        Assert.True(controller.IsRateLimited(ipA));  // 第 3 次，触发限流
        // ipB 不受 ipA 影响
        Assert.False(controller.IsRateLimited(ipB));
    }

    [Fact]
    public void RateLimit_Zero_Means_No_Limit()
    {
        var options = new NtpServerOptions { RateLimitPerMinute = 0 };
        using var controller = new NetworkAccessController(options);
        var ip = IPAddress.Parse("10.0.0.1");
        for (int i = 0; i < 10; i++)
        {
            Assert.False(controller.IsRateLimited(ip));
        }
    }
}

public class NetworkMatcherTests
{
    [Fact]
    public void Matches_Exact_IPv4()
    {
        Assert.True(NetworkMatcher.Matches(IPAddress.Parse("192.168.1.5"), "192.168.1.5"));
        Assert.False(NetworkMatcher.Matches(IPAddress.Parse("192.168.1.6"), "192.168.1.5"));
    }

    [Fact]
    public void Matches_Cidr()
    {
        Assert.True(NetworkMatcher.Matches(IPAddress.Parse("10.0.0.1"), "10.0.0.0/8"));
        Assert.False(NetworkMatcher.Matches(IPAddress.Parse("11.0.0.1"), "10.0.0.0/8"));
    }

    [Fact]
    public void Matches_Garbage_Returns_False()
    {
        Assert.False(NetworkMatcher.Matches(IPAddress.Parse("10.0.0.1"), "garbage"));
        Assert.False(NetworkMatcher.Matches(IPAddress.Parse("10.0.0.1"), ""));
    }

    [Fact]
    public void Matches_Ipv6_Cidr()
    {
        Assert.True(NetworkMatcher.Matches(IPAddress.Parse("2001:db8::1"), "2001:db8::/32"));
        Assert.False(NetworkMatcher.Matches(IPAddress.Parse("2001:db9::1"), "2001:db8::/32"));
        Assert.True(NetworkMatcher.Matches(IPAddress.Parse("2001:db8::1"), "2001:db8::1/128"));
    }

    [Fact]
    public void OutOfRange_Prefix_Returns_False_Without_Crash()
    {
        // /200 超过 IPv4 地址位长，不应崩溃，也不应误匹配
        Assert.False(NetworkMatcher.Matches(IPAddress.Parse("10.0.0.1"), "10.0.0.0/200"));
    }
}