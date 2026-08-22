namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// 单条代理连接日志记录（仅记录元数据，不记录传输内容）。
/// </summary>
public class ConnectionLog
{
    /// <summary>连接建立时间</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>所属隧道名称</summary>
    public string TunnelName { get; set; } = string.Empty;

    /// <summary>代理类型</summary>
    public ProxyType ProxyType { get; set; }

    /// <summary>本地客户端地址</summary>
    public string ClientEndpoint { get; set; } = string.Empty;

    /// <summary>目标地址:端口</summary>
    public string TargetEndpoint { get; set; } = string.Empty;

    /// <summary>上传字节数</summary>
    public long BytesSent { get; set; }

    /// <summary>下载字节数</summary>
    public long BytesReceived { get; set; }

    /// <summary>连接持续时间</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>连接状态</summary>
    public string Status { get; set; } = "Success";
}
