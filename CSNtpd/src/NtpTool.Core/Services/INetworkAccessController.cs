using System.Net;

namespace NtpTool.Core.Services;

/// <summary>
/// 网络访问控制：判断某个客户端 IP 是否允许访问。用于服务端白名单/黑名单策略。
/// 对应需求文档第 5.3.7 节。
/// </summary>
public interface INetworkAccessController
{
    /// <summary>判断给定时点（用于限流）与来源 IP 是否允许访问。</summary>
    bool IsAllowed(IPAddress clientIp);

    /// <summary>判断当前是否触发单 IP 限流（每 IP 每分钟请求数上限）。</summary>
    bool IsRateLimited(IPAddress clientIp);
}