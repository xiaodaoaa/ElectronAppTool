namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// SSH 服务器配置。
/// 敏感字段（密码、私钥 Passphrase、代理密码）使用 DPAPI 加密后存储。
/// </summary>
public class SshServerProfile
{
    /// <summary>配置唯一标识</summary>
    public Guid Id { get; set; }

    /// <summary>配置名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SSH 服务器地址</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SSH 端口</summary>
    public int Port { get; set; } = 22;

    /// <summary>SSH 用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>认证方式</summary>
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;

    /// <summary>密码（DPAPI 加密后存储）</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>私钥文件路径</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>私钥 Passphrase（DPAPI 加密后存储）</summary>
    public string EncryptedPassphrase { get; set; } = string.Empty;

    /// <summary>内嵌私钥内容（可选，DPAPI 加密后存储）</summary>
    public string EncryptedPrivateKeyContent { get; set; } = string.Empty;

    /// <summary>代理监听地址</summary>
    public string ListenAddress { get; set; } = "127.0.0.1";

    /// <summary>SOCKS5 监听端口</summary>
    public int Socks5ListenPort { get; set; } = 1080;

    /// <summary>HTTP 监听端口</summary>
    public int HttpListenPort { get; set; } = 8118;

    /// <summary>是否启用代理层认证</summary>
    public bool EnableProxyAuth { get; set; }

    /// <summary>代理认证用户名</summary>
    public string ProxyUsername { get; set; } = string.Empty;

    /// <summary>代理认证密码（DPAPI 加密后存储）</summary>
    public string EncryptedProxyPassword { get; set; } = string.Empty;

    /// <summary>连接超时（秒）</summary>
    public int ConnectTimeoutSec { get; set; } = 15;

    /// <summary>Keep-Alive 间隔（秒）</summary>
    public int KeepAliveIntervalSec { get; set; } = 30;

    /// <summary>最大重连次数（-1 = 无限）</summary>
    public int MaxReconnectAttempts { get; set; } = -1;

    /// <summary>重连间隔（秒）</summary>
    public int ReconnectDelaySec { get; set; } = 5;
}
