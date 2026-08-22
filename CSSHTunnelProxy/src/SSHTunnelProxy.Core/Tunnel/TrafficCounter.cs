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
    private DateTime _lastSampleUtc = DateTime.UtcNow;

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

    /// <summary>滚动速率窗口：返回当前采样秒内累加的字节统计，并更新采样基线。</summary>
    public (double UploadBytesPerSec, double DownloadBytesPerSec) Sample()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSampleUtc).TotalSeconds;
            _lastSampleUtc = now;

            // 推进采样桶。
            _sampleIndex = (_sampleIndex + 1) % SampleCount;
            _uploadSamples[_sampleIndex] = 0;
            _downloadSamples[_sampleIndex] = 0;

            long up = 0, down = 0;
            foreach (var s in _uploadSamples)
                up += s;
            foreach (var s in _downloadSamples)
                down += s;

            var windowSec = Math.Max(SampleCount, elapsed);
            return (up / windowSec, down / windowSec);
        }
    }

    public long TotalBytesSent { get { lock (_lock) return _totalBytesSent; } }

    public long TotalBytesReceived { get { lock (_lock) return _totalBytesReceived; } }

    public int ActiveConnections { get { lock (_lock) return (int)_activeConnections; } }

    public long TotalConnections { get { lock (_lock) return _totalConnections; } }
}
