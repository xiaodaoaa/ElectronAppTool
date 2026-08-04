namespace NtpTool.Core.Models;

/// <summary>一次客户端同步的结果。见需求文档第 9.4 节。</summary>
public sealed class NtpSyncResult
{
    public DateTime SyncTimeUtc { get; set; }
    public string Server { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double OffsetMs { get; set; }
    public double RoundTripDelayMs { get; set; }
    public byte Stratum { get; set; }
    public byte LeapIndicator { get; set; }
    public string? ReferenceId { get; set; }
    public bool SystemTimeChanged { get; set; }
    public string? ErrorMessage { get; set; }

    public static NtpSyncResult Failed(string server, string error)
    {
        return new NtpSyncResult
        {
            SyncTimeUtc = DateTime.UtcNow,
            Server = server,
            Success = false,
            ErrorMessage = error
        };
    }
}