namespace NtpTool.Core.Services;

/// <summary>服务端状态，见需求文档第 5.7.2 节状态机。</summary>
public enum ServerState
{
    Stopped,
    Starting,
    Listening,
    Error
}

/// <summary>
/// NTP 服务端服务：监听 UDP、响应客户端请求、统计、访问控制。对应需求文档第 5.3 节。
/// </summary>
public interface INtpServerService : IDisposable
{
    /// <summary>状态变化事件。</summary>
    event EventHandler<ServerState>? StateChanged;

    /// <summary>统计信息更新事件（收到请求时触发），用于界面实时刷新。</summary>
    event EventHandler? StatisticsChanged;

    /// <summary>当前状态。</summary>
    ServerState State { get; }

    /// <summary>启动监听。</summary>
    Task StartAsync();

    /// <summary>停止监听。</summary>
    Task StopAsync();

    /// <summary>使用新服务端配置（下次启动生效）。</summary>
    void ApplyOptions(NtpTool.Core.Models.NtpServerOptions options);

    /// <summary>访问控制器（白名单/限流）。</summary>
    NtpTool.Core.Services.INetworkAccessController AccessController { get; }

    /// <summary>服务端统计信息（线程安全）。</summary>
    NtpTool.Core.Models.NtpServerStatistics Statistics { get; }
}