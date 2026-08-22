namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// 隧道状态变更事件参数。
/// </summary>
public class TunnelStateEventArgs : EventArgs
{
    public TunnelStateEventArgs(TunnelState newState, string? message = null)
    {
        NewState = newState;
        Message = message;
    }

    /// <summary>新状态</summary>
    public TunnelState NewState { get; }

    /// <summary>附加消息（如错误信息）</summary>
    public string? Message { get; }
}
