using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.Core.Tests;

public class ConfigValidatorTests
{
    [Fact]
    public void Valid_Config_Passes()
    {
        var settings = new AppSettings();
        settings.Client.Servers.Add(new NtpServerConfig { Host = "time.windows.com", Port = 123, Enabled = true });
        var validator = new ConfigValidator();
        var result = validator.Validate(settings);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_Server_Port_Is_Reported()
    {
        var settings = new AppSettings();
        settings.Client.Servers.Add(new NtpServerConfig { Host = "time.windows.com", Port = 99999 });
        var result = new ConfigValidator().Validate(settings);
        Assert.Contains(result.Errors, e => e.Contains("端口"));
    }

    [Fact]
    public void Empty_Server_Host_Is_Reported()
    {
        var settings = new AppSettings();
        settings.Client.Servers.Add(new NtpServerConfig { Host = "" });
        var result = new ConfigValidator().Validate(settings);
        Assert.Contains(result.Errors, e => e.Contains("地址不能为空"));
    }

    [Fact]
    public void AutoSync_With_No_Active_Server_Warns()
    {
        var settings = new AppSettings();
        settings.Client.EnableAutoSync = true;
        settings.Client.Servers.Clear();
        var result = new ConfigValidator().Validate(settings);
        Assert.Contains(result.Errors, e => e.Contains("没有启用的服务器"));
    }

    [Fact]
    public void Invalid_Timeout_Is_Reported()
    {
        var settings = new AppSettings();
        settings.Client.TimeoutMs = 50000;
        var result = new ConfigValidator().Validate(settings);
        Assert.Contains(result.Errors, e => e.Contains("超时"));
    }

    [Fact]
    public void Normalize_Fixes_OutOfRange_Stratum()
    {
        var settings = new AppSettings();
        settings.Server.Stratum = 99;
        new ConfigValidator().Normalize(settings);
        Assert.Equal((byte)2, settings.Server.Stratum);
    }

    [Fact]
    public void Normalize_Fixes_Invalid_Port()
    {
        var settings = new AppSettings();
        settings.Server.Port = 0;
        new ConfigValidator().Normalize(settings);
        Assert.Equal(123, settings.Server.Port);
    }

    [Fact]
    public void Normalize_Fixes_Invalid_ListenAddress()
    {
        var settings = new AppSettings();
        settings.Server.ListenAddress = "not-an-ip";
        new ConfigValidator().Normalize(settings);
        Assert.Equal("0.0.0.0", settings.Server.ListenAddress);
    }

    [Fact]
    public void Valid_Network_Rules()
    {
        Assert.True(ConfigValidator.IsValidNetwork("192.168.1.0/24"));
        Assert.True(ConfigValidator.IsValidNetwork("10.0.0.5"));
        Assert.True(ConfigValidator.IsValidNetwork("172.16.10.0/24"));
    }

    [Fact]
    public void Ipv6_Networks_Validated_With_128_Prefix()
    {
        // IPv6 前缀可到 /128，不应被当作非法
        Assert.True(ConfigValidator.IsValidNetwork("2001:db8::/32"));
        Assert.True(ConfigValidator.IsValidNetwork("2001:db8::1/128"));
        Assert.True(ConfigValidator.IsValidNetwork("fe80::/64"));
        // IPv6 超过 /128 仍属非法
        Assert.False(ConfigValidator.IsValidNetwork("2001:db8::/129"));
    }

    [Fact]
    public void Invalid_Network_Rules_Detected()
    {
        Assert.False(ConfigValidator.IsValidNetwork("999.168.1.0/24"));
        Assert.False(ConfigValidator.IsValidNetwork("192.168.1.0/33"));
        Assert.False(ConfigValidator.IsValidNetwork("not-an-ip"));
        Assert.False(ConfigValidator.IsValidNetwork(""));
        Assert.False(ConfigValidator.IsValidNetwork("192.168.1.0/"));
    }
}