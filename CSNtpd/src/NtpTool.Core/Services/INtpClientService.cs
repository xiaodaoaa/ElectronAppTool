using NtpTool.Core.Models;

namespace NtpTool.Core.Services;

/// <summary>客户端同步状态，见需求文档第 5.7.1 节状态机。</summary>
public enum ClientSyncState
{
    Stopped,
    Idle,
    Syncing,
    Success,
    Failed,
    Warning
}

/// <summary>
/// NTP 客户端服务：立即同步、自动同步调度、故障切换。对应需求文档第 5.2 节。
/// </summary>
public interface INtpClientService : IDisposable
{
    /// <summary>同步状态变化事件。</summary>
    event EventHandler<ClientSyncState>? StateChanged;

    /// <summary>同步完成事件（含成功与失败）。</summary>
    event EventHandler<NtpSyncResult>? SyncCompleted;

    /// <summary>当前状态。</summary>
    ClientSyncState State { get; }

    /// <summary>最近一次同步结果。</summary>
    NtpSyncResult? LastResult { get; }

    /// <summary>连续失败次数。</summary>
    int ConsecutiveFailures { get; }

    /// <summary>立即执行一次同步（异步），成功后按策略触发系统时间更新。</summary>
    Task<NtpSyncResult> SyncNowAsync(CancellationToken cancellationToken = default);

    /// <summary>启动定时同步。</summary>
    void StartAutoSync();

    /// <summary>停止定时同步。</summary>
    void StopAutoSync();

    /// <summary>是否正在定时同步。</summary>
    bool IsAutoSyncRunning { get; }

    /// <summary>使用新的客户端配置（下次同步生效）。</summary>
    void ApplyOptions(NtpClientOptions options);
}