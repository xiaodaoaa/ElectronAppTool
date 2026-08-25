using Microsoft.Extensions.Logging;
using SSHTunnelProxy.Core.Tunnel;

namespace SSHTunnelProxy.Core.Utils;

/// <summary>
/// 双向流桥接工具：将 client 与 target 双向透传，任一方向结束则取消另一方。
/// </summary>
public static class StreamRelay
{
    private const int BufferSize = 81920;

    /// <summary>
    /// 在两个流之间双向透传数据，直到任一侧关闭或出现异常。
    /// </summary>
    /// <param name="client">代理客户端流</param>
    /// <param name="target">隧道目标流</param>
    /// <param name="counter">流量计数（可为 null）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="logger">运行日志器（可为 null）</param>
    /// <returns>各方向透传的字节数，供连接日志记录。</returns>
    public static async Task<StreamRelayResult> RelayAsync(
        Stream client,
        Stream target,
        TrafficCounter? counter,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var upstream = CopyAsync(client, target, isClientToTarget: true, counter, linkedCts.Token, logger);
        var downstream = CopyAsync(target, client, isClientToTarget: false, counter, linkedCts.Token, logger);

        // 任一侧结束（EOF 或取消）即取消另一侧。
        await Task.WhenAny(upstream, downstream);
        linkedCts.Cancel();

        // 等待两者结束后，若两侧均已正常 EOF 则无异常返回；否则抛出一个异常以便上层记录。
        await Task.WhenAll(upstream, downstream);

        return new StreamRelayResult(upstream.Result, downstream.Result);
    }

    private static async Task<long> CopyAsync(
        Stream source,
        Stream destination,
        bool isClientToTarget,
        TrafficCounter? counter,
        CancellationToken token,
        ILogger? logger)
    {
        var buffer = new byte[BufferSize];
        long total = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), token);
                total += read;
                if (counter is not null)
                {
                    if (isClientToTarget)
                        counter.AddSent(read);
                    else
                        counter.AddReceived(read);
                }
            }

            // 单向 EOF：对端应停止，由链路关闭处理（若目标未关闭，则在此关闭写入半侧）。
            if (isClientToTarget)
                await destination.FlushAsync(token);
        }
        catch (OperationCanceledException)
        {
            // 正常取消。
        }
        catch (IOException ex)
        {
            // 一方断开。
            logger?.LogDebug(ex, "流透传 IO 异常（{Dir}）", isClientToTarget ? "上行" : "下行");
        }
        catch (ObjectDisposedException ex)
        {
            // 流已释放。
            logger?.LogDebug(ex, "流透传对象已释放（{Dir}）", isClientToTarget ? "上行" : "下行");
        }

        return total;
    }
}

/// <summary>一次双向透传的字节统计结果。</summary>
public readonly record struct StreamRelayResult(long BytesUpstream, long BytesDownstream);
