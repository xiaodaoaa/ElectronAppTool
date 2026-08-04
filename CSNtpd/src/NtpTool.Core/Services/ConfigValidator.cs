using System.Net;
using NtpTool.Core.Models;

namespace NtpTool.Core.Services;

/// <summary>配置校验结果。</summary>
public sealed class ConfigValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
}

/// <summary>
/// 配置校验器，按需求文档第 5.5.3 节规则校验配置：服务器地址、端口、周期、
/// 超时、Stratum、监听地址、白名单等。非法值应被替换为默认值并记录错误。
/// </summary>
public sealed class ConfigValidator
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;

    public ConfigValidationResult Validate(AppSettings settings)
    {
        var result = new ConfigValidationResult();

        ValidateClient(settings.Client, result);
        ValidateServer(settings.Server, result);
        ValidateLog(settings.Log, result);

        return result;
    }

    /// <summary>将非法配置原地修正为默认值，返回是否发生了修正。</summary>
    public bool Normalize(AppSettings settings)
    {
        var original = settings.Clone();
        bool changed = false;

        var client = settings.Client;
        if (client.SyncIntervalMinutes < 1)
        {
            client.SyncIntervalMinutes = 30;
            changed = true;
        }
        if (client.TimeoutMs is < 100 or > 30_000)
        {
            client.TimeoutMs = 3000;
            changed = true;
        }
        if (client.RetryCount < 0)
        {
            client.RetryCount = 3;
            changed = true;
        }
        if (client.RetryIntervalMs < 0)
        {
            client.RetryIntervalMs = 10_000;
            changed = true;
        }
        if (client.MaxAcceptableDelayMs <= 0)
        {
            client.MaxAcceptableDelayMs = 10_000;
            changed = true;
        }

        var server = settings.Server;
        if (server.Port is < MinPort or > MaxPort)
        {
            server.Port = 123;
            changed = true;
        }
        if (server.Stratum is < 1 or > 15)
        {
            server.Stratum = 2;
            changed = true;
        }
        if (server.ListenAddress.Length == 0 || !IPAddress.TryParse(server.ListenAddress, out _))
        {
            server.ListenAddress = "0.0.0.0";
            changed = true;
        }
        if (server.RateLimitPerMinute < 0)
        {
            server.RateLimitPerMinute = 120;
            changed = true;
        }

        return changed && !AreEqual(original, settings);
    }

    private static bool AreEqual(AppSettings a, AppSettings b)
    {
        return a.Client.SyncIntervalMinutes == b.Client.SyncIntervalMinutes
            && a.Client.TimeoutMs == b.Client.TimeoutMs
            && a.Client.RetryCount == b.Client.RetryCount
            && a.Client.RetryIntervalMs == b.Client.RetryIntervalMs
            && a.Server.Port == b.Server.Port
            && a.Server.Stratum == b.Server.Stratum
            && a.Server.ListenAddress == b.Server.ListenAddress
            && a.Server.RateLimitPerMinute == b.Server.RateLimitPerMinute;
    }

    private static void ValidateClient(NtpClientOptions client, ConfigValidationResult result)
    {
        if (client.SyncIntervalMinutes < 1)
        {
            result.Errors.Add("同步周期必须不小于 1 分钟（换算为秒不小于 10 秒），已使用默认值 30。");
        }
        if (client.TimeoutMs is < 100 or > 30_000)
        {
            result.Errors.Add("超时时间必须在 100ms - 30000ms 之间，已使用默认值 3000。");
        }

        var activeServers = client.Servers.Where(s => s.Enabled).ToList();
        if (client.EnableAutoSync && activeServers.Count == 0)
        {
            result.Errors.Add("已启用自动同步，但没有启用的服务器。");
        }

        foreach (var server in client.Servers)
        {
            if (string.IsNullOrWhiteSpace(server.Host))
            {
                result.Errors.Add("服务器地址不能为空。");
                continue;
            }
            if (server.Port is < MinPort or > MaxPort)
            {
                result.Errors.Add($"服务器 {server.Host} 的端口无效：{server.Port}。");
            }
        }
    }

    private static void ValidateServer(NtpServerOptions server, ConfigValidationResult result)
    {
        if (server.Port is < MinPort or > MaxPort)
        {
            result.Errors.Add($"监听端口无效：{server.Port}。");
        }
        if (server.Stratum is < 1 or > 15)
        {
            result.Errors.Add($"Stratum 必须在 1-15 之间：{server.Stratum}。");
        }
        if (string.IsNullOrWhiteSpace(server.ListenAddress) || !IPAddress.TryParse(server.ListenAddress, out _))
        {
            result.Errors.Add($"监听地址无效：{server.ListenAddress}。");
        }
        foreach (var network in server.AllowedNetworks)
        {
            if (!IsValidNetwork(network))
            {
                result.Errors.Add($"白名单条目无效：{network}。");
            }
        }
    }

    private static void ValidateLog(LogSettings log, ConfigValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(log.Directory))
        {
            result.Errors.Add("日志目录不能为空。");
        }
        if (log.MaxFileSizeMb < 1)
        {
            result.Errors.Add("日志单文件大小不能小于 1MB。");
        }
        if (log.RetentionDays < 0)
        {
            result.Errors.Add("日志保留天数不能为负。");
        }
    }

    /// <summary>校验 CIDR（如 192.168.1.0/24）或 IP（如 10.0.0.5）是否合法。</summary>
    public static bool IsValidNetwork(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains('/'))
        {
            var parts = trimmed.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }
            if (!IPAddress.TryParse(parts[0].Trim(), out IPAddress? address))
            {
                return false;
            }

            // 按地址族区分前缀上限：IPv4 最大 32，IPv6 最大 128
            int maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            return int.TryParse(parts[1].Trim(), out int prefix)
                && prefix is >= 0 and <= 128
                && prefix <= maxPrefix;
        }

        return IPAddress.TryParse(trimmed, out _);
    }
}
