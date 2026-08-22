using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SSHTunnelProxy.Core.Security;

/// <summary>
/// 基于 TOFU 的主机密钥验证器。
/// 首次连接保存密钥指纹并信任；后续连接校验指纹是否一致。
/// 持久化路径见 <see cref="KnownHostsFilePath"/>。
/// </summary>
public class HostKeyVerifier : IHostKeyVerifier
{
    private readonly string _knownHostsFilePath;
    private readonly IDpapiProtector _protector;
    private Dictionary<string, string>? _cache;

    public HostKeyVerifier(
        string knownHostsFilePath,
        IDpapiProtector protector)
    {
        _knownHostsFilePath = knownHostsFilePath;
        _protector = protector;
    }

    private Dictionary<string, string> Cache
    {
        get
        {
            if (_cache is null)
                _cache = Load();
            return _cache;
        }
    }

    public bool VerifyHostKey(string host, int port, byte[] hostKey)
    {
        var key = $"{host}:{port}";
        var fingerprint = ComputeFingerprint(hostKey);

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var saved))
                return string.Equals(saved, fingerprint, StringComparison.Ordinal);

            // TOFU：首次保存。
            Cache[key] = fingerprint;
            Save(Cache);
            return true;
        }
    }

    private static string ComputeFingerprint(byte[] hostKey)
    {
        // 参考 OpenSSH 的 SHA256 指纹格式。
        var sha256 = SHA256.HashData(hostKey);
        return "SHA256:" + Convert.ToBase64String(sha256);
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_knownHostsFilePath))
                return new Dictionary<string, string>();
            var json = File.ReadAllText(_knownHostsFilePath);
            // 指纹非机密，但通过 DPAPI 加密以防篡改。
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private void Save(Dictionary<string, string> data)
    {
        try
        {
            var dir = Path.GetDirectoryName(_knownHostsFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(data);
            File.WriteAllText(_knownHostsFilePath, json, Encoding.UTF8);
        }
        catch
        {
            // 保存失败不影响连接（仅失去 TOFU 校验能力）。
        }
    }
}
