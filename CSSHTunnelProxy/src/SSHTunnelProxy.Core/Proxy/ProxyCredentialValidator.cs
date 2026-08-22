using SSHTunnelProxy.Core.Security;
using System.Security.Cryptography;
using System.Text;

namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// 代理层用户名/密码校验器。密码以固定盐哈希存储，避免明文比对。
/// </summary>
public sealed class ProxyCredentialValidator : IProxyCredentialValidator
{
    // 固定应用盐（代理密码不涉及高价值长期凭据，重点在于不落明文）。
    private static readonly byte[] Salt = "SSHTunnelProxy::ProxyAuth"u8.ToArray();

    private readonly string _username;
    private readonly string _passwordHash;

    public ProxyCredentialValidator(string username, string password)
    {
        _username = username;
        _passwordHash = Hash(password);
    }

    public bool Validate(string username, string password)
    {
        if (!string.Equals(_username, username, StringComparison.Ordinal))
            return false;
        return ConstantTimeEquals(_passwordHash, Hash(password));
    }

    private static string Hash(string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var data = new byte[Salt.Length + valueBytes.Length];
        Salt.CopyTo(data, 0);
        valueBytes.CopyTo(data, Salt.Length);
        return Convert.ToHexString(SHA256.HashData(data));
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
