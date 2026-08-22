namespace SSHTunnelProxy.Core.Security;

/// <summary>
/// 主机密钥验证器：首次连接采用 TOFU（Trust On First Use）模式，后续严格校验。
/// </summary>
public interface IHostKeyVerifier
{
    /// <summary>
    /// 验证主机密钥。首次遇到该主机时保存密钥并信任（TOFU）；
    /// 之后若密钥与已保存值不一致则拒绝。
    /// </summary>
    /// <param name="host">主机地址</param>
    /// <param name="port">SSH 端口</param>
    /// <param name="hostKey">服务端提供的主机密钥字节</param>
    /// <returns>是否信任该主机密钥</returns>
    bool VerifyHostKey(string host, int port, byte[] hostKey);
}
