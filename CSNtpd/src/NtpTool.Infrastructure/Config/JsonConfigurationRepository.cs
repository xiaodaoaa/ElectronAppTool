using System.Text.Json;
using System.Text.Json.Serialization;
using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.Infrastructure.Config;

/// <summary>
/// JSON 配置文件持久化实现。对应需求文档第 5.5.2 / 5.5.4 节。
/// 文件不存在或损坏时返回默认配置。
/// </summary>
public sealed class JsonConfigurationRepository : IConfigurationRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public string FilePath { get; }

    public JsonConfigurationRepository(string? filePath = null)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, AppSettings.DefaultFileName)
            : Path.GetFullPath(filePath);
    }

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            // 首次启动：写入默认配置到磁盘，便于用户查看与编辑
            var defaults = CreateDefaultSettings();
            Save(defaults, out _);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return loaded ?? new AppSettings();
        }
        catch
        {
            // 配置损坏时返回默认配置，不向上抛导致启动失败
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings, out string? error)
    {
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(FilePath, json);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public AppSettings ResetToDefault()
    {
        var defaults = CreateDefaultSettings();
        if (Save(defaults, out _))
        {
            return defaults;
        }

        return defaults;
    }

    /// <summary>创建带常用上游服务器的默认配置。</summary>
    private static AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings();
        settings.Client.Servers.Add(new NtpServerConfig
        {
            Host = "time.windows.com",
            Port = 123,
            Priority = 1,
            Enabled = true,
            Remark = "Windows 时间服务器"
        });
        settings.Client.Servers.Add(new NtpServerConfig
        {
            Host = "pool.ntp.org",
            Port = 123,
            Priority = 2,
            Enabled = true,
            Remark = "NTP 池"
        });
        return settings;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}