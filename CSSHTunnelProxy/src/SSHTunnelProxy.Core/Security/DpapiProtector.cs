using System.Security.Cryptography;
using System.Text;

namespace SSHTunnelProxy.Core.Security;

/// <summary>
/// 基于 Windows DPAPI（ProtectedData）的敏感数据保护器。
/// 使用 CurrentUser 作用域，仅当前用户可解密。
/// </summary>
public class DpapiProtector : IDpapiProtector
{
    // 附加熵，防止相同明文产生相同密文的外部推定。
    private static readonly byte[] Entropy = "SSHTunnelProxy::DPAPI"u8.ToArray();

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        var cipherBytes = Convert.FromBase64String(ciphertext);
        var decrypted = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }
}
