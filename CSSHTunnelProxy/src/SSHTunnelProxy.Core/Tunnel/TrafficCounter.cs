namespace SSHTunnelProxy.Core.Tunnel;

/// <summary>
/// 流量计数器：累计字节数 + 滑动窗口速率 + 连接数统计。
/// 线程安全，供多并发连接共享累加。
/// </summary>
public class TrafficCounter
{
    private readonly object _lock = new();
    private long _totalBytesSent;
    private long _totalBytesReceived;
    private long _activeConnections;
    private long _totalConnections;

    // 滑动窗口（固定桶），用于速率估算。
    private readonly long[] _uploadSamples = new long[SampleCount];
    private readonly long[] _downloadSamples = new long[SampleCount];
    private int _sampleIndex;

    private const int SampleCount = 5; // 最近 5 秒

    /// <summary>记录单次发送字节数。</summary>
    public void AddSent(long bytes)
    {
        if (bytes <= 0)
            return;
        lock (_lock)
        {
            _totalBytesSent += bytes;
            _uploadSamples[_sampleIndex] += bytes;
        }
    }

    /// <summary>记录单次接收字节数。</summary>
    public void AddReceived(long bytes)
    {
        if (bytes <= 0)
            return;
        lock (_lock)
        {
            _totalBytesReceived += bytes;
            _downloadSamples[_sampleIndex] += bytes;
        }
    }

    /// <summary>活跃连接 +1。</summary>
    public void AddConnection()
    {
        lock (_lock)
        {
            _activeConnections++;
            _totalConnections++;
        }
    }

    /// <summary>活跃连接 -1。</summary>
    public void RemoveConnection()
    {
        lock (_lock)
        {
            if (_activeConnections > 0)
                _activeConnections--;
        }
    }

    /// <summary>
    /// 滚动速率窗口：返回近 <see cref="SampleCount"/> 个采样周期的平均速率，并滚动采样桶。
    /// 先对当前窗口求和（含正在累积的当前桶），再推进并清零最老桶，
    /// 使稳态下每个桶各含一个完整周期的数据，避免系统性低估。
    /// </summary>
    public (double UploadBytesPerSec, double DownloadBytesPerSec) Sample()
    {
        lock (_lock)
        {
            long up = 0, down = 0;
            foreach (var s in _uploadSamples)
                up += s;
            foreach (var s in _downloadSamples)
                down += s;

            // 推进到下一桶并清零最老桶，供下一周期累积。
            _sampleIndex = (_sampleIndex + 1) % SampleCount;
            _uploadSamples[_sampleIndex] = 0;
            _downloadSamples[_sampleIndex] = 0;

            var windowSec = (double)SampleCount;
            return (up / windowSec, down / windowSec);
        }
    }

    public long TotalBytesSent { get { lock (_lock) return _totalBytesSent; } }

    public long TotalBytesReceived { get { lock (_lock) return _totalBytesReceived; } }

    public int ActiveConnections { get { lock (_lock) return (int)_activeConnections; } }

    public long TotalConnections { get { lock (_lock) return _totalConnections; } }
}
