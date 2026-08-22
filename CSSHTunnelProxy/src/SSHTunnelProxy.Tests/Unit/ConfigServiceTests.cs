using FluentAssertions;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Security;
using SSHTunnelProxy.Core.Services;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "stp_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new ConfigService(new DpapiProtector(), _tempDir);
    }

    [Fact]
    public async Task SaveAndLoadProfiles_RoundTrips()
    {
        var profile = new SshServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "测试服务器",
            Host = "example.com",
            Port = 22,
            Username = "user",
            AuthMethod = AuthMethod.Password,
            EncryptedPassword = new DpapiProtector().Encrypt("secret"),
        };

        await _service.SaveProfilesAsync([profile]);
        var loaded = await _service.LoadProfilesAsync();

        loaded.Should().HaveCount(1);
        loaded[0].Id.Should().Be(profile.Id);
        loaded[0].Host.Should().Be("example.com");
        loaded[0].AuthMethod.Should().Be(AuthMethod.Password);
        loaded[0].EncryptedPassword.Should().Be(profile.EncryptedPassword);
    }

    [Fact]
    public async Task LoadSettings_ReturnsDefaultWhenMissing()
    {
        var settings = await _service.LoadSettingsAsync();

        settings.Should().NotBeNull();
        settings.LogRetentionDays.Should().Be(30);
    }

    [Fact]
    public async Task SaveAndLoadSettings_RoundTrips()
    {
        var settings = new AppSettings
        {
            LogRetentionDays = 7,
            MinimizeToTray = false,
        };

        await _service.SaveSettingsAsync(settings);
        var loaded = await _service.LoadSettingsAsync();

        loaded.LogRetentionDays.Should().Be(7);
        loaded.MinimizeToTray.Should().BeFalse();
    }

    [Fact]
    public async Task CorruptedProfilesFile_ReturnsEmpty()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "profiles.json"), "{ invalid json !!!");

        var loaded = await _service.LoadProfilesAsync();

        loaded.Should().BeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
