using System.Collections.Concurrent;

namespace NtpTool.Core.Models;

/// <summary>
/// 线程安全的 NTP 服务端统计信息。见需求文档第 9.6 节与第 5.3.6 节。
/// </summary>
public sealed class NtpServerStatistics
{
    private long _totalRequests;
    private long _validRequests;
    private long _invalidRequests;
    private long _rejectedRequests;
    private long _rateLimitedRequests;

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long ValidRequests => Interlocked.Read(ref _validRequests);
    public long InvalidRequests => Interlocked.Read(ref _invalidRequests);
    public long RejectedRequests => Interlocked.Read(ref _rejectedRequests);
    public long RateLimitedRequests => Interlocked.Read(ref _rateLimitedRequests);

    /// <summary>当前每分钟请求计数（按 IP）。</summary>
    public ConcurrentDictionary<string, int> RequestsPerIpMinute { get; } = new();

    public string? LastClientAddress { get; private set; }
    public DateTime? LastRequestTimeUtc { get; private set; }

    private readonly object _lock = new();

    public void RecordTotalRequest(string? clientAddress)
    {
        Interlocked.Increment(ref _totalRequests);
        if (clientAddress is not null)
        {
            lock (_lock)
            {
                LastClientAddress = clientAddress;
                LastRequestTimeUtc = DateTime.UtcNow;
            }
        }
    }

    public void RecordValidRequest() => Interlocked.Increment(ref _validRequests);

    public void RecordInvalidRequest() => Interlocked.Increment(ref _invalidRequests);

    public void RecordRejectedRequest() => Interlocked.Increment(ref _rejectedRequests);

    public void RecordRateLimitedRequest()
    {
        Interlocked.Increment(ref _rateLimitedRequests);
        Interlocked.Increment(ref _rejectedRequests);
    }
}