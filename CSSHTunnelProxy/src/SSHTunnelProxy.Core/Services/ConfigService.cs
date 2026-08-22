using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 配置文件路径与 JSON 持久化实现。
/// 存储位置：%APPDATA%\SSHTunnelProxy\
/// </summary>
public sealed class ConfigService : IConfigService
{
    private readonly string _basePath;
    private readonly IDpapiProtector _protector;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ConfigService(IDpapiProtector protector)
        : this(protector, GetDefaultBasePath())
    {
    }

    public ConfigService(IDpapiProtector protector, string basePath)
    {
        _protector = protector;
        _basePath = basePath;
    }

    private string ProfilesPath => Path.Combine(_basePath, "profiles.json");
    private string SettingsPath => Path.Combine(_basePath, "settings.json");

    public async Task<IList<SshServerProfile>> LoadProfilesAsync()
    {
        if (!File.Exists(ProfilesPath))
            return new List<SshServerProfile>();

        try
        {
            var json = await File.ReadAllTextAsync(ProfilesPath).ConfigureAwait(false);
            var items = JsonSerializer.Deserialize<PersistedProfiles>(json, JsonOptions);
            return items?.Profiles ?? new List<SshServerProfile>();
        }
        catch
        {
            // 配置损坏时返回空，避免崩溃。
            return new List<SshServerProfile>();
        }
    }

    public async Task SaveProfilesAsync(IList<SshServerProfile> profiles)
    {
        EnsureDirectory();
        var payload = new PersistedProfiles { Profiles = profiles.ToList() };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(ProfilesPath, json).ConfigureAwait(false);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        EnsureDirectory();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json).ConfigureAwait(false);
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(_basePath);
    }

    private static string GetDefaultBasePath() => AppPaths.Root;

    /// <summary>持久化容器，便于 future 扩展。</summary>
    private sealed class PersistedProfiles
    {
        public List<SshServerProfile> Profiles { get; set; } = new();
    }
}
