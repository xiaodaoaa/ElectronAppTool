namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// SSH 认证方式。
/// </summary>
public enum AuthMethod
{
    /// <summary>密码认证</summary>
    Password,

    /// <summary>私钥认证</summary>
    PrivateKey,

    /// <summary>键盘交互认证</summary>
    KeyboardInteractive,
}
