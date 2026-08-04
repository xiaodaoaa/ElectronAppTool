using System.Net;
using System.Net.Sockets;
using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Core.Ntp;

namespace NtpTool.Core.Services;

/// <summary>
/// NTP 服务端实现：监听 UDP、解析客户端请求、构造并返回响应、更新统计。
/// 对应需求文档第 5.3 节与第 8.4 节流程。
/// </summary>
public sealed class NtpServerService : INtpServerService
{
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private NtpServerOptions _options;
    private NetworkAccessController? _accessController;
    private Socket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private DateTime _referenceTimestampUtc;

    public event EventHandler<ServerState>? StateChanged;
    public ServerState State { get; private set; } = ServerState.Stopped;
    public INetworkAccessController AccessController => _accessController ?? throw new InvalidOperationException("服务端未启动。");
    public NtpServerStatistics Statistics { get; } = new();

    /// <summary>每当服务端统计信息更新（收到请求）时触发，用于界面实时刷新。</summary>
    public event EventHandler? StatisticsChanged;

    public NtpServerService(NtpServerOptions options, IAppLogger logger, TimeProvider? timeProvider = null)
    {
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void ApplyOptions(NtpServerOptions options)
    {
        _options = options;
    }

    public Task StartAsync()
    {
        if (State == ServerState.Listening || State == ServerState.Starting)
        {
            return Task.CompletedTask;
        }

        SetState(ServerState.Starting);

        try
        {
            _accessController?.Dispose();
            _accessController = new NetworkAccessController(_options);
            _socket = CreateBoundSocket(out IPAddress ip, out int port);
            _logger.Information("NtpServer", $"NTP Server 正在监听 {ip}:{port}。");

            _cts = new CancellationTokenSource();
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_socket, _cts.Token));
            // 记录启动时刻作为"最近一次时间源更新时间"（Reference Timestamp），
            // 避免 ntpdate 等客户端因 reference time 为 0 判定"服务器长时间未同步"。
            _referenceTimestampUtc = _timeProvider.GetUtcNow().UtcDateTime;
            SetState(ServerState.Listening);
            _logger.Information("NtpServer", "NTP Server 已启动。");
            return Task.CompletedTask;
        }
        catch (SocketException ex)
        {
            HandleBindError(ex);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error("NtpServer", $"服务端启动失败：{ex.Message}", ex);
            _socket?.Dispose();
            _socket = null;
            SetState(ServerState.Error);
            return Task.CompletedTask;
        }
    }

    private Socket CreateBoundSocket(out IPAddress boundIp, out int boundPort)
    {
        IPAddress listenAddress = ResolveListenAddress(out boundIp);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = 0; // 等待(receive 阻塞在 async loop)
        try
        {
            socket.Bind(new IPEndPoint(listenAddress, _options.Port));
            boundPort = ((IPEndPoint)socket.LocalEndPoint!).Port;
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private IPAddress ResolveListenAddress(out IPAddress boundIp)
    {
        boundIp = IPAddress.Parse(_options.ListenAddress);
        return boundIp;
    }

    private void HandleBindError(SocketException ex)
    {
        _logger.Error("NtpServer",
            $"无法启动 NTP Server：UDP {_options.Port} 端口可能被占用或需要管理员权限。请停止 w32time 服务，或修改本工具监听端口。原始错误：{ex.Message}",
            ex);
        _socket?.Dispose();
        _socket = null;
        SetState(ServerState.Error);
    }

    private async Task ReceiveLoopAsync(Socket socket, CancellationToken token)
    {
        byte[] buffer = new byte[NtpPacketCodec.PacketSize];
        var remote = new IPEndPoint(IPAddress.Any, 0);

        while (!token.IsCancellationRequested)
        {
            SocketReceiveFromResult result;
            try
            {
                ArraySegment<byte> segment = new(buffer);
                result = await socket.ReceiveFromAsync(segment, SocketFlags.None, remote, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.Error("NtpServer", $"接收请求发生 Socket 异常：{ex.Message}", ex);
                continue;
            }

            IPEndPoint from = (IPEndPoint)result.RemoteEndPoint!;
            int length = result.ReceivedBytes;
            byte[] received = buffer.AsSpan(0, length).ToArray();
            // 每请求独立处理，避免 overread
            ProcessRequestAsync(from, received);
        }
    }

    private void ProcessRequestAsync(IPEndPoint from, byte[] data)
    {
        Statistics.RecordTotalRequest(from.Address.ToString());

        var controller = AccessController;
        if (!controller.IsAllowed(from.Address))
        {
            Statistics.RecordRejectedRequest();
            RaiseStatisticsChanged();
            if (_options.LogRejectedRequests)
            {
                _logger.Warning("NtpServer", $"拒绝来自 {from.Address} 的请求：不在白名单内。");
            }

            return;
        }

        if (controller.IsRateLimited(from.Address))
        {
            Statistics.RecordRateLimitedRequest();
            RaiseStatisticsChanged();
            _logger.Warning("NtpServer", $"来自 {from.Address} 的请求触发限流，已丢弃。");
            return;
        }

        byte[] response;
        try
        {
            NtpPacket request = NtpPacketCodec.Decode(data);
            if (request.Mode != (byte)NtpMode.Client)
            {
                Statistics.RecordInvalidRequest();
                RaiseStatisticsChanged();
                return;
            }

            response = BuildResponse(request);
            Statistics.RecordValidRequest();
            RaiseStatisticsChanged();
        }
        catch (NtpPacketException ex)
        {
            Statistics.RecordInvalidRequest();
            RaiseStatisticsChanged();
            if (_options.LogRequests)
            {
                _logger.Warning("NtpServer", $"来自 {from.Address} 的非法报文被丢弃：{ex.Message}");
            }

            return;
        }

        try
        {
            if (_socket is not null)
            {
                _socket.SendToAsync(new ArraySegment<byte>(response), SocketFlags.None, from).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("NtpServer", $"发送响应失败：{ex.Message}", ex);
        }

        if (_options.LogRequests)
        {
            _logger.Debug("NtpServer", $"已响应来自 {from.Address}:{from.Port} 的 NTP 请求。");
        }
    }

    private byte[] BuildResponse(NtpPacket request)
    {
        var response = new NtpPacket
        {
            LeapIndicator = _options.LeapIndicator,
            VersionNumber = request.VersionNumber is >= 3 and <= 4 ? request.VersionNumber : (byte)4,
            Mode = (byte)NtpMode.Server,
            Stratum = _options.Stratum,
            Poll = request.Poll == 0 ? _options.Poll : request.Poll,
            Precision = unchecked((byte)_options.Precision),
            RootDelay = _options.RootDelay,
            RootDispersion = _options.RootDispersion,
            ReferenceId = EncodeReferenceId(_options.ReferenceId),
            // Reference Timestamp 用启动时刻，表示参考源最近一次更新时间，
            // 不能用 0，否则客户端判定服务器长时间未同步而拒绝。
            ReferenceTimestamp = _referenceTimestampUtc,
            // 逐位回显请求的 Originate Timestamp，避免 DateTime 往返导致精度丢失，
            // 否则 ntpdate 等客户端会因 pkt.org != peer.xmt 而拒绝应答。
            RawOriginateTimestamp = request.RawTransmitTimestamp,
            OriginateTimestamp = request.TransmitTimestamp,
            ReceiveTimestamp = _timeProvider.GetUtcNow().UtcDateTime,
            TransmitTimestamp = _timeProvider.GetUtcNow().UtcDateTime
        };

        return NtpPacketCodec.Encode(response);
    }

    private static uint EncodeReferenceId(string referenceId)
    {
        string raw = string.IsNullOrWhiteSpace(referenceId) ? "LOCAL" : referenceId.Trim();
        if (raw.Length > 4)
        {
            raw = raw.Substring(0, 4);
        }

        uint value = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            value = (value << 8) | (byte)raw[i];
        }

        return value;
    }

    public async Task StopAsync()
    {
        if (State == ServerState.Stopped)
        {
            return;
        }

        _cts?.Cancel();
        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // 忽略停止竞态
            }
        }

        _socket?.Dispose();
        _socket = null;
        _cts?.Dispose();
        _cts = null;
        _receiveLoop = null;
        _accessController?.Dispose();
        _accessController = null;

        SetState(ServerState.Stopped);
        _logger.Information("NtpServer", "NTP Server 已停止。");
    }

    private void SetState(ServerState newState)
    {
        if (State == newState)
        {
            return;
        }

        State = newState;
        StateChanged?.Invoke(this, newState);
    }

    private void RaiseStatisticsChanged()
    {
        StatisticsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}