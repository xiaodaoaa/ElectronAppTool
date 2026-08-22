using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Proxy;
using System.Collections.Concurrent;

namespace SSHTunnelProxy.Tests.Helpers;

/// <summary>
/// 内存版连接日志接收器：收集代理服务器产生的连接日志，供断言使用。
/// </summary>
public sealed class CollectingSink : IConnectionSink
{
    private readonly ConcurrentQueue<ConnectionLog> _logs = new();

    public IReadOnlyList<ConnectionLog> Logs => _logs.ToArray();

    public Task RecordConnectionAsync(ConnectionLog log)
    {
        _logs.Enqueue(log);
        return Task.CompletedTask;
    }

    /// <summary>阻塞等待满足谓词的连接日志出现，超时抛异常。</summary>
    public async Task<ConnectionLog> WaitForAsync(Func<ConnectionLog, bool> predicate, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var match = _logs.FirstOrDefault(predicate);
            if (match is not null)
                return match;
            await Task.Delay(20);
        }
        throw new TimeoutException($"未在 {timeoutMs}ms 内找到满足条件的连接日志。");
    }
}

/// <summary>内存版代理凭证校验器。</summary>
public sealed class FixedCredentialValidator : IProxyCredentialValidator
{
    public required string ExpectedUser { get; init; }
    public required string ExpectedPassword { get; init; }

    public bool Validate(string username, string password)
        => username == ExpectedUser && password == ExpectedPassword;
}
