using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using NtpTool.Core.Logging;
using NtpTool.Core.Models;

namespace NtpTool.Infrastructure.Logging;

/// <summary>
/// 文件日志实现：按大小滚动、异步批量写入、按保留天数清理旧文件。
/// 对应需求文档第 5.6 节。
/// </summary>
public sealed class FileLogger : IAppLogger, IDisposable
{
    private readonly string _directory;
    private readonly int _maxFileSizeBytes;
    private readonly int _retentionDays;
    private readonly Channel<LogEntry> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Thread _writerThread;
    private bool _disposed;

    public LogLevel MinimumLevel { get; set; }

    public event EventHandler<LogEntry>? EntryWritten;

    public FileLogger(LogSettings settings)
    {
        _directory = ResolveDirectory(settings.Directory);
        _maxFileSizeBytes = Math.Max(1, settings.MaxFileSizeMb) * 1024 * 1024;
        _retentionDays = settings.RetentionDays;
        MinimumLevel = LogLevelExtensions.ParseOrDefault(settings.Level);

        Directory.CreateDirectory(_directory);

        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _cts = new CancellationTokenSource();
        _writerThread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "NtpToolFileLogger"
        };
        _writerThread.Start();
    }

    public void Log(LogLevel level, string module, string message, Exception? exception = null)
    {
        if (level < MinimumLevel || _disposed)
        {
            return;
        }

        var entry = new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            Module = module,
            Message = message,
            Exception = exception?.ToString()
        };

        _channel.Writer.TryWrite(entry);
        EntryWritten?.Invoke(this, entry);
    }

    private void WriteLoop()
    {
        try
        {
            foreach (LogEntry entry in _channel.Reader.ReadAllAsync(_cts.Token).ToBlockingEnumerable())
            {
                WriteEntry(entry);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch
        {
            // 写日志失败不应影响主程序
        }
    }

    private void WriteEntry(LogEntry entry)
    {
        try
        {
            EnforceRetention();
            string filePath = GetCurrentFilePath();
            string line = Format(entry);
            File.AppendAllText(filePath, line, Encoding.UTF8);
        }
        catch
        {
            // 忽略单个日志写入失败
        }
    }

    private string GetCurrentFilePath()
    {
        string filePath = Path.Combine(_directory, $"ntp-tool-{DateTime.UtcNow:yyyyMMdd}.log");
        var info = new FileInfo(filePath);
        if (info.Exists && info.Length >= _maxFileSizeBytes)
        {
            string rotated = Path.Combine(_directory, $"ntp-tool-{DateTime.UtcNow:yyyyMMdd}-{DateTime.UtcNow:HHmmss}.log");
            try
            {
                File.Move(filePath, rotated);
            }
            catch
            {
                // 轮转失败则继续写原文件
            }
        }

        return filePath;
    }

    private void EnforceRetention()
    {
        if (_retentionDays <= 0)
        {
            return;
        }

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            foreach (string file in Directory.EnumerateFiles(_directory, "ntp-tool-*.log"))
            {
                var created = File.GetCreationTimeUtc(file);
                if (created < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }

    public static string Format(LogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append(entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        sb.Append(" [").Append(entry.Level.ToShortName()).Append(']');
        sb.Append(" [").Append(entry.Module).Append("] ");
        sb.Append(entry.Message);
        if (!string.IsNullOrEmpty(entry.Exception))
        {
            sb.Append(" | ").Append(entry.Exception);
        }

        return sb.Append(Environment.NewLine).ToString();
    }

    /// <summary>
    /// 解析日志目录。相对路径锚定到可执行文件所在目录，避免受进程当前工作目录影响。
    /// </summary>
    private static string ResolveDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = "logs";
        }

        return Path.IsPathRooted(directory)
            ? Path.GetFullPath(directory)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, directory));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _writerThread.Join(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }
}