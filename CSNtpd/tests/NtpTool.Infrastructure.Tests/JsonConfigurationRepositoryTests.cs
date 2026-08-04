using NtpTool.Core.Models;
using NtpTool.Infrastructure.Config;

namespace NtpTool.Infrastructure.Tests;

public class JsonConfigurationRepositoryTests
{
    private readonly string _tempDir;

    public JsonConfigurationRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NtpToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        string path = Path.Combine(_tempDir, "roundtrip.json");
        var repo = new JsonConfigurationRepository(path);
        var settings = new AppSettings();
        settings.Client.EnableAutoSync = true;
        settings.Client.SyncIntervalMinutes = 15;
        settings.Client.Servers.Add(new NtpServerConfig { Host = "pool.ntp.org", Port = 123, Priority = 1, Enabled = true });
        settings.Server.EnableServer = true;
        settings.Server.Port = 10123;

        Assert.True(repo.Save(settings, out _));

        var loaded = repo.Load();
        Assert.True(loaded.Client.EnableAutoSync);
        Assert.Equal(15, loaded.Client.SyncIntervalMinutes);
        Assert.Single(loaded.Client.Servers);
        Assert.Equal("pool.ntp.org", loaded.Client.Servers[0].Host);
        Assert.True(loaded.Server.EnableServer);
        Assert.Equal(10123, loaded.Server.Port);
    }

    [Fact]
    public void Missing_File_Returns_Defaults()
    {
        string path = Path.Combine(_tempDir, "missing.json");
        var repo = new JsonConfigurationRepository(path);
        var settings = repo.Load();
        Assert.False(settings.Client.EnableAutoSync);
        Assert.False(settings.Server.EnableServer);
    }

    [Fact]
    public void Corrupt_File_Returns_Defaults()
    {
        string path = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(path, "{ not valid json !!!");
        var repo = new JsonConfigurationRepository(path);
        var settings = repo.Load();
        Assert.NotNull(settings);
        Assert.False(settings.Server.EnableServer);
    }

    [Fact]
    public void Json_Serialization_Is_Consistent()
    {
        string path = Path.Combine(_tempDir, "consistent.json");
        var repo = new JsonConfigurationRepository(path);
        var settings = new AppSettings();
        settings.Server.Stratum = 3;
        settings.Server.ReferenceId = "LOCAL";
        settings.Log.Level = "Debug";

        repo.Save(settings, out _);
        string firstContent = File.ReadAllText(path);
        var loaded = repo.Load();
        repo.Save(loaded, out _);
        string secondContent = File.ReadAllText(path);

        var loadedAgain = repo.Load();
        Assert.Equal(3, loadedAgain.Server.Stratum);
        Assert.Equal("LOCAL", loadedAgain.Server.ReferenceId);
        Assert.Equal("Debug", loadedAgain.Log.Level);
    }
}