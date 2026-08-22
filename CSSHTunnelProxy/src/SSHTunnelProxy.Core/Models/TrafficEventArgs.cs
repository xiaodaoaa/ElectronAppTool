namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// 流量统计更新事件参数。
/// </summary>
public class TrafficEventArgs : EventArgs
{
    public TrafficEventArgs(long totalBytesSent, long totalBytesReceived)
    {
        TotalBytesSent = totalBytesSent;
        TotalBytesReceived = totalBytesReceived;
    }

    /// <summary>累计上传字节数</summary>
    public long TotalBytesSent { get; }

    /// <summary>累计下载字节数</summary>
    public long TotalBytesReceived { get; }
}
