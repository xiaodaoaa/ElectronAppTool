namespace NtpTool.Core.Models;

/// <summary>NTP 客户端配置。见需求文档第 9.2 节。</summary>
public sealed class NtpClientOptions
{
    public bool EnableAutoSync { get; set; }
    public int SyncIntervalMinutes { get; set; } = 30;
    public int TimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 3;
    public int RetryIntervalMs { get; set; } = 10_000;
    public bool RunOnceOnStart { get; set; }
    public bool ApplySystemTime { get; set; }
    public double AutoApplyThresholdMs { get; set; } = 500;
    public double MaxAllowedOffsetMs { get; set; } = 30_000;
    public double MaxAcceptableDelayMs { get; set; } = 10_000;
    public int FailureWarningThreshold { get; set; } = 3;
    public List<NtpServerConfig> Servers { get; set; } = new();

    public NtpClientOptions Clone()
    {
        return new NtpClientOptions
        {
            EnableAutoSync = EnableAutoSync,
            SyncIntervalMinutes = SyncIntervalMinutes,
            TimeoutMs = TimeoutMs,
            RetryCount = RetryCount,
            RetryIntervalMs = RetryIntervalMs,
            RunOnceOnStart = RunOnceOnStart,
            ApplySystemTime = ApplySystemTime,
            AutoApplyThresholdMs = AutoApplyThresholdMs,
            MaxAllowedOffsetMs = MaxAllowedOffsetMs,
            MaxAcceptableDelayMs = MaxAcceptableDelayMs,
            FailureWarningThreshold = FailureWarningThreshold,
            Servers = Servers.Select(s => new NtpServerConfig
            {
                Host = s.Host,
                Port = s.Port,
                Priority = s.Priority,
                Enabled = s.Enabled,
                Remark = s.Remark
            }).ToList()
        };
    }
}