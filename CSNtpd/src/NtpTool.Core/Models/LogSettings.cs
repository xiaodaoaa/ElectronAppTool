namespace NtpTool.Core.Models;

/// <summary>日志配置。见需求文档第 5.6 节与 5.5.1 节。</summary>
public sealed class LogSettings
{
    public string Level { get; set; } = "Information";
    public string Directory { get; set; } = "logs";
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetentionDays { get; set; } = 30;
    public bool LogUdpDetails { get; set; }

    public LogSettings Clone()
    {
        return new LogSettings
        {
            Level = Level,
            Directory = Directory,
            MaxFileSizeMb = MaxFileSizeMb,
            RetentionDays = RetentionDays,
            LogUdpDetails = LogUdpDetails
        };
    }
}