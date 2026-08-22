namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// SSH 隧道连接状态。
/// </summary>
public enum TunnelState
{
    /// <summary>未连接</summary>
    Disconnected,

    /// <summary>正在连接</summary>
    Connecting,

    /// <summary>已连接</summary>
    Connected,

    /// <summary>正在重连</summary>
    Reconnecting,

    /// <summary>错误</summary>
    Error,
}
