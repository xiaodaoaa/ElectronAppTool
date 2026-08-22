namespace SSHTunnelProxy.Core.Security;

/// <summary>
/// 敏感数据保护器接口，支持对数据进行加密/解密。
/// </summary>
public interface IDpapiProtector
{
    /// <summary>加密明文，返回经过编码的密文。</summary>
    string Encrypt(string plaintext);

    /// <summary>解密经过编码的密文，返回明文。</summary>
    string Decrypt(string ciphertext);
}
