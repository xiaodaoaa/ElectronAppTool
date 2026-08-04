using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace NtpTool.Core.Services;

/// <summary>
/// IP 与 CIDR 匹配工具。
/// </summary>
public static class NetworkMatcher
{
    /// <summary>判断 IP 是否匹配 IP 或 CIDR 形式的网络描述。</summary>
    public static bool Matches(IPAddress address, string network)
    {
        if (string.IsNullOrWhiteSpace(network))
        {
            return false;
        }

        var mapped = address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;

        string trimmed = network.Trim();
        if (trimmed.Contains('/'))
        {
            string[] parts = trimmed.Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0].Trim(), out IPAddress? baseAddress))
            {
                return false;
            }

            baseAddress = baseAddress.AddressFamily == AddressFamily.InterNetworkV6 && baseAddress.IsIPv4MappedToIPv6
                ? baseAddress.MapToIPv4()
                : baseAddress;

            if (!int.TryParse(parts[1].Trim(), out int prefix) || baseAddress.AddressFamily != mapped.AddressFamily)
            {
                return false;
            }

            byte[] baseBytes = baseAddress.GetAddressBytes();
            byte[] targetBytes = mapped.GetAddressBytes();

            // 防御性边界：前缀不可能超过地址长度（IPv4≤32 / IPv6≤128）
            int maxBits = baseBytes.Length * 8;
            if (prefix < 0 || prefix > maxBits)
            {
                return false;
            }

            int fullBytes = prefix / 8;
            int remainingBits = prefix % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (baseBytes[i] != targetBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((baseBytes[fullBytes] & mask) != (targetBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }

        return IPAddress.TryParse(trimmed, out IPAddress? exact) && exact.Equals(mapped);
    }
}

/// <summary>
/// 网络访问控制器实现：白名单（CIDR / IP）与单 IP 每分钟限流。
/// 对应需求文档第 5.3.7 / 5.3.8 节。
/// </summary>
public sealed class NetworkAccessController : INetworkAccessController, IDisposable
{
    private readonly NtpTool.Core.Models.NtpServerOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitWindow> _rateWindows = new();
    private readonly Timer _cleanupTimer;

    public NetworkAccessController(NtpTool.Core.Models.NtpServerOptions options)
    {
        _options = options;
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public bool IsAllowed(IPAddress clientIp)
    {
        if (_options.AllowAllClients)
        {
            return true;
        }

        foreach (string network in _options.AllowedNetworks)
        {
            if (NetworkMatcher.Matches(clientIp, network))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsRateLimited(IPAddress clientIp)
    {
        if (_options.RateLimitPerMinute <= 0)
        {
            return false;
        }

        string key = MapToV4(clientIp).ToString();
        RateLimitWindow window = _rateWindows.GetOrAdd(key, _ => new RateLimitWindow());
        return !window.TryIncrement(_options.RateLimitPerMinute);
    }

    private void Cleanup()
    {
        DateTime now = DateTime.UtcNow;
        foreach (var kvp in _rateWindows)
        {
            if (now - kvp.Value.StartUtc > TimeSpan.FromMinutes(2))
            {
                _rateWindows.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static IPAddress MapToV4(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        return address;
    }

    private sealed class RateLimitWindow
    {
        public DateTime StartUtc { get; private set; } = DateTime.UtcNow;
        public int Count { get; private set; }

        public bool TryIncrement(int limit)
        {
            if (DateTime.UtcNow - StartUtc >= TimeSpan.FromMinutes(1))
            {
                StartUtc = DateTime.UtcNow;
                Count = 0;
            }

            if (Count >= limit)
            {
                return false;
            }

            Count++;
            return true;
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}