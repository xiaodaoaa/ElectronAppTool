using FluentAssertions;
using SSHTunnelProxy.Core.Tunnel;
using SSHTunnelProxy.Core.Utils;
using System.IO.Pipelines;
using System.Text;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

/// <summary>
/// 针对双向透传 StreamRelay 的字节统计单元测试。
/// </summary>
public class StreamRelayTests
{
    [Fact]
    public async Task RelayAsync_ReportsDirectionalByteCounts()
    {
        var dataToTarget = Encoding.UTF8.GetBytes("客户端 -> 目标的载荷");
        var dataToClient = Encoding.UTF8.GetBytes("目标 -> 客户端的载荷");

        // client 侧：Read 产出 dataToTarget（作为"客户端发送"的数据）。
        var clientRead = new Pipe();
        await clientRead.Writer.WriteAsync(dataToTarget);
        await clientRead.Writer.CompleteAsync();
        var clientWrite = new Pipe();

        // target 侧：Read 产出 dataToClient（作为"目标返回"的数据）。
        var targetRead = new Pipe();
        await targetRead.Writer.WriteAsync(dataToClient);
        await targetRead.Writer.CompleteAsync();
        var targetWrite = new Pipe();

        // 组装双向流：
        //   client = { 读到 clientRead 的内容，写入 clientWrite }
        //   target = { 读到 targetRead 的内容，写入 targetWrite }
        using var client = new BidirectionalPipeStream(clientRead.Reader, clientWrite.Writer);
        using var target = new BidirectionalPipeStream(targetRead.Reader, targetWrite.Writer);

        var counter = new TrafficCounter();

        var result = await StreamRelay.RelayAsync(client, target, counter, CancellationToken.None);

        // 方向统计
        result.BytesUpstream.Should().Be(dataToTarget.Length);
        result.BytesDownstream.Should().Be(dataToClient.Length);

        // 流量计数器也应累计
        counter.TotalBytesSent.Should().Be(dataToTarget.Length);
        counter.TotalBytesReceived.Should().Be(dataToClient.Length);
    }

    [Fact]
    public async Task RelayAsync_SingleDirection_OtherSideZero()
    {
        var dataToTarget = Encoding.UTF8.GetBytes("仅客户端发送，目标无响应");

        var clientRead = new Pipe();
        await clientRead.Writer.WriteAsync(dataToTarget);
        await clientRead.Writer.CompleteAsync();
        var clientWrite = new Pipe();

        // target 侧不产生任何数据：立即 EOF。
        var targetRead = new Pipe();
        await targetRead.Writer.CompleteAsync();
        var targetWrite = new Pipe();

        using var client = new BidirectionalPipeStream(clientRead.Reader, clientWrite.Writer);
        using var target = new BidirectionalPipeStream(targetRead.Reader, targetWrite.Writer);

        var result = await StreamRelay.RelayAsync(client, target, null, CancellationToken.None);

        result.BytesUpstream.Should().Be(dataToTarget.Length);
        result.BytesDownstream.Should().Be(0);
    }

    /// <summary>把 PipeReader/PipeWriter 组合成可读写的 Stream。</summary>
    private sealed class BidirectionalPipeStream : Stream
    {
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;
        private bool _disposed;

        public BidirectionalPipeStream(PipeReader reader, PipeWriter writer)
        {
            _reader = reader;
            _writer = writer;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException(
                "请使用异步 ReadAsync（测试通过任务管线执行）。");

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var readResult = await _reader.ReadAsync(ct);
            if (readResult.Buffer.IsEmpty)
                return 0;

            var copy = (int)Math.Min(readResult.Buffer.Length, buffer.Length);
            var dest = buffer.Span;
            var remaining = copy;
            foreach (var segment in readResult.Buffer)
            {
                if (remaining <= 0)
                    break;
                var take = Math.Min(segment.Length, remaining);
                segment.Span[..take].CopyTo(dest);
                dest = dest[take..];
                remaining -= take;
            }
            _reader.AdvanceTo(readResult.Buffer.GetPosition(copy));
            return copy;
        }

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException(
                "请使用异步 WriteAsync（测试通过任务管线执行）。");

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken ct = default)
        {
            await _writer.WriteAsync(buffer, ct);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _reader.Complete();
                    _writer.Complete();
                }
            }
            base.Dispose(disposing);
        }
    }
}
