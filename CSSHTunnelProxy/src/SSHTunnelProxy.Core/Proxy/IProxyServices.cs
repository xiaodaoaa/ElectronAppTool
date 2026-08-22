using SSHTunnelProxy.Core.Models;

namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// 代理连接日志接收器：代理服务器在连接结束时回写连接元数据。
/// </summary>
public interface IConnectionSink
{
    /// <summary>记录一条代理连接日志。</summary>
    Task RecordConnectionAsync(ConnectionLog log);
}

/// <summary>
/// 代理层用户名/密码校验器。
/// </summary>
public interface IProxyCredentialValidator
{
    /// <summary>校验代理认证凭证是否有效。</summary>
    bool Validate(string username, string password);
}
