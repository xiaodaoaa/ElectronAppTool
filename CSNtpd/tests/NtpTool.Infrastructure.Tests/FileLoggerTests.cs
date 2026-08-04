using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Infrastructure.Logging;

namespace NtpTool.Infrastructure.Tests;

public class FileLoggerTests
{
    private readonly string _tempDir;

    public FileLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NtpToolLogTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Log_Writes_To_File()
    {
        var settings = new LogSettings { Directory = _tempDir, Level = "Information" };
        using IAppLogger logger = new FileLogger(settings);
        logger.Information("TestModule", "hello world");
        // 等待后台线程刷盘
        SpinWaitForFiles();

        string[] files = Directory.GetFiles(_tempDir, "ntp-tool-*.log");
        Assert.NotEmpty(files);
        string content = File.ReadAllText(files[0]);
        Assert.Contains("[INFO]", content);
        Assert.Contains("TestModule", content);
        Assert.Contains("hello world", content);
    }

    [Fact]
    public void Log_Raises_EntryWritten()
    {
        var settings = new LogSettings { Directory = _tempDir, Level = "Information" };
        using IAppLogger logger = new FileLogger(settings);
        LogEntry? received = null;
        logger.EntryWritten += (_, e) => received = e;

        logger.Information("M", "msg");
        Assert.NotNull(received);
        Assert.Equal("msg", received.Message);
        Assert.Equal(LogLevel.Information, received.Level);
    }

    [Fact]
    public void BelowMinimumLevel_Is_Filtered()
    {
        var settings = new LogSettings { Directory = _tempDir, Level = "Error" };
        using IAppLogger logger = new FileLogger(settings);
        LogEntry? received = null;
        logger.EntryWritten += (_, e) => received = e;

        logger.Information("M", "should-not-appear");
        Assert.Null(received);
    }

    [Fact]
    public void ParseOrDefault_Maps_Levels()
    {
        Assert.Equal(LogLevel.Trace, LogLevelExtensions.ParseOrDefault("trace"));
        Assert.Equal(LogLevel.Information, LogLevelExtensions.ParseOrDefault("information"));
        Assert.Equal(LogLevel.Warning, LogLevelExtensions.ParseOrDefault("warn"));
        Assert.Equal(LogLevel.Error, LogLevelExtensions.ParseOrDefault("error"));
        Assert.Equal(LogLevel.Information, LogLevelExtensions.ParseOrDefault("bogus"));
        Assert.Equal(LogLevel.Information, LogLevelExtensions.ParseOrDefault(""));
    }

    private void SpinWaitForFiles()
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (Directory.GetFiles(_tempDir, "ntp-tool-*.log").Length > 0)
            {
                Thread.Sleep(50);
                return;
            }

            Thread.Sleep(20);
        }
    }

    private void Cleanup()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            // 忽略清理失败
        }
    }
}