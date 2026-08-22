using SSHTunnelProxy.Core.Models;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 隧道管理器：负责多隧道实例的生命周期管理与自动重连。
/// </summary>
public interface ITunnelManager
{
    /// <summary>启动一个隧道（含 SSH 连接 + 代理监听）。</summary>
    Task<TunnelContext> StartTunnelAsync(SshServerProfile profile);

    /// <summary>停止指定隧道。</summary>
    Task StopTunnelAsync(Guid tunnelId);

    /// <summary>重启指定隧道。</summary>
    Task RestartTunnelAsync(Guid tunnelId);

    /// <summary>停止所有隧道。</summary>
    Task StopAllAsync();

    /// <summary>获取运行中的隧道上下文。</summary>
    IReadOnlyCollection<TunnelContext> GetActiveTunnels();

    /// <summary>隧道状态变更事件。</summary>
    event EventHandler<TunnelEventArgs>? TunnelStateChanged;
}
