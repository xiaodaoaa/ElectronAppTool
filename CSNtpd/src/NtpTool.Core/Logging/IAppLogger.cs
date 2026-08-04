namespace NtpTool.Core.Logging;

/// <summary>一条日志记录。</summary>
public sealed class LogEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

/// <summary>
/// 简单的应用日志抽象。UI 日志面板、文件日志分别实现此接口并订阅 <see cref="EntryWritten"/>。
/// 每次 <c>Log</c> 调用同时写入本地文件并触发 <see cref="EntryWritten"/> 事件。
/// </summary>
public interface IAppLogger : IDisposable
{
    /// <summary>当写入一条日志时触发（用于 UI 面板刷新）。</summary>
    event EventHandler<LogEntry>? EntryWritten;

    LogLevel MinimumLevel { get; set; }

    void Log(LogLevel level, string module, string message, Exception? exception = null);

    void Trace(string module, string message) => Log(LogLevel.Trace, module, message);
    void Debug(string module, string message) => Log(LogLevel.Debug, module, message);
    void Information(string module, string message) => Log(LogLevel.Information, module, message);
    void Warning(string module, string message) => Log(LogLevel.Warning, module, message, null);
    void Error(string module, string message, Exception? ex = null) => Log(LogLevel.Error, module, message, ex);
    void Fatal(string module, string message, Exception? ex = null) => Log(LogLevel.Fatal, module, message, ex);
}