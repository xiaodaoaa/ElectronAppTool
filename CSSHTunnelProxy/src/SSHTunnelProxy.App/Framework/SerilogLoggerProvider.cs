using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using MelLogger = Microsoft.Extensions.Logging.ILogger;

namespace SSHTunnelProxy.App.Framework;

/// <summary>
/// 把 Microsoft.Extensions.Logging 的 ILogger 桥接到静态 Serilog Log，
/// 使 Core 层依赖的 ILogger&lt;T&gt; 与 App 的 Serilog 文件日志统一输出，
/// 同时无需引入额外的 Serilog.Extensions.Logging 包。
/// </summary>
public sealed class SerilogLoggerProvider : ILoggerProvider
{
    /// <summary>单例：避免每次 Resolve ILogger&lt;T&gt; 时新建实例。</summary>
    public static readonly SerilogLoggerProvider Instance = new();

    public MelLogger CreateLogger(string categoryName)
        => new BridgeLogger(categoryName);

    public void Dispose()
    {
    }

    /// <summary>把 ILogger 调用转发到 Serilog 的桥接实现。</summary>
    private sealed class BridgeLogger : MelLogger
    {
        private readonly string _category;

        public BridgeLogger(string category)
        {
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => Serilog.Log.IsEnabled(ToSerilogLevel(logLevel));

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var level = ToSerilogLevel(logLevel);
            if (!Serilog.Log.IsEnabled(level))
                return;

            var message = formatter(state, exception);
            Serilog.Log.Write(level, exception, "[{Category}] {Message}", _category, message);
        }

        private static LogEventLevel ToSerilogLevel(LogLevel level) => level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }
}
