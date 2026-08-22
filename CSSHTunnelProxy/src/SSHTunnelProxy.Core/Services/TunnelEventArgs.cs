using SSHTunnelProxy.Core.Models;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 隧道管理器事件参数：包含隧道 ID 与相关配置。
/// </summary>
public class TunnelEventArgs : EventArgs
{
    public TunnelEventArgs(Guid tunnelId, SshServerProfile? profile = null)
    {
        TunnelId = tunnelId;
        Profile = profile;
    }

    /// <summary>隧道 ID。</summary>
    public Guid TunnelId { get; }

    /// <summary>相关配置（可能为 null）。</summary>
    public SshServerProfile? Profile { get; }

    /// <summary>当前状态（若适用）。</summary>
    public TunnelState State { get; init; }
}
