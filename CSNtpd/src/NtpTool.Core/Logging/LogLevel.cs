namespace NtpTool.Core.Logging;

/// <summary>日志级别。见需求文档第 5.6.2 节。</summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}

public static class LogLevelExtensions
{
    public static string ToShortName(this LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Fatal => "FATAL",
        _ => "INFO"
    };

    /// <summary>解析文本为日志级别，失败返回默认级别 Information。</summary>
    public static LogLevel ParseOrDefault(string? value, LogLevel fallback = LogLevel.Information)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "fatal" => LogLevel.Fatal,
            _ => fallback
        };
    }
}