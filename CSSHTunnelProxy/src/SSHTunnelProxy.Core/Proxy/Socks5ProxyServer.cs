using Microsoft.Extensions.Logging;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Tunnel;
using SSHTunnelProxy.Core.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// SOCKS5 代理服务器：监听本地端口，解析 SOCKS5 CONNECT 请求，
/// 通过 SSH 隧道转发到目标地址。
/// </summary>
public sealed class Socks5ProxyServer : IProxyServer
{
    private readonly ProxyServerOptions _options;
    private readonly ISshTunnelTransport _transport;
    private readonly ILogger? _logger;

    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private volatile int _activeClients;
    private volatile bool _running;

    public Socks5ProxyServer(ISshTunnelTransport transport, ProxyServerOptions options)
    {
        _transport = transport;
        _options = options;
        _logger = options.Logger;
    }

    public ProxyType Type => ProxyType.Socks5;

    public int BoundPort { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            return;

        var address = IPAddress.Parse(_options.ListenAddress);
        _listener = new TcpListener(address, _options.ListenPort);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _running = true;

        _logger?.LogInformation("SOCKS5 代理监听已启动 {Tunnel} @{Address}:{Port}", _options.TunnelName, _options.ListenAddress, BoundPort);
        _ = AcceptLoopAsync();
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_running)
            return;

        _running = false;
        _cts.Cancel();
        _listener?.Stop();

        // 等待活跃客户端优雅结束（限时 5s）。
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_activeClients > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        _listener = null;
        _logger?.LogInformation("SOCKS5 代理监听已停止 {Tunnel}", _options.TunnelName);
    }

    private async Task AcceptLoopAsync()
    {
        while (_running && !_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                break;
            }

            Interlocked.Increment(ref _activeClients);
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        Stream? channel = null;
        var started = DateTime.UtcNow;
        var log = new ConnectionLog
        {
            Timestamp = DateTime.Now,
            TunnelName = _options.TunnelName,
            ProxyType = ProxyType.Socks5,
            ClientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? string.Empty,
        };

        _options.Traffic?.AddConnection();
        try
        {
            using var clientStream = client.GetStream();
            channel = await RunProtocolAsync(clientStream, log);
            log.Status = "Success";
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            _logger?.LogWarning(ex, "SOCKS5 连接处理失败 {Tunnel} 客户端 {Client} 目标 {Target}",
                _options.TunnelName, log.ClientEndpoint, log.TargetEndpoint);
        }
        finally
        {
            _options.Traffic?.RemoveConnection();
            log.Duration = DateTime.UtcNow - started;

            if (channel is not null)
            {
                try { channel.Dispose(); } catch { }
            }

            var sink = _options.ConnectionSink;
            if (sink is not null)
            {
                try { await sink.RecordConnectionAsync(log); }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "回写连接日志失败 {Tunnel}", _options.TunnelName);
                }
            }

            _logger?.LogDebug("SOCKS5 连接结束 {Tunnel} {Client}→{Target} 上传 {Up} 下载 {Down} 时长 {Duration}ms 状态 {Status}",
                _options.TunnelName, log.ClientEndpoint, log.TargetEndpoint,
                log.BytesSent, log.BytesReceived, (long)log.Duration.TotalMilliseconds, log.Status);

            client.Dispose();
            Interlocked.Decrement(ref _activeClients);
        }
    }

    /// <summary>执行 SOCKS5 协议流程，返回需透传的隧道流。</summary>
    private async Task<Stream> RunProtocolAsync(Stream clientStream, ConnectionLog log)
    {
        // ① 握手
        var methods = await ReadHandshakeAsync(clientStream);
        var useAuth = _options.EnableProxyAuth && methods.Contains(Socks5Protocol.MethodUsernamePassword);
        var noAuth = methods.Contains(Socks5Protocol.MethodNoAuth);

        if (!noAuth && !useAuth)
        {
            await clientStream.WriteAsync(Socks5Protocol.BuildHandshakeReply(Socks5Protocol.MethodNoAcceptable));
            _logger?.LogWarning("SOCKS5 握手失败：无可接受认证方法 {Tunnel} 客户端 {Client}", _options.TunnelName, log.ClientEndpoint);
            throw new SocksProtocolException("无可接受的认证方法。");
        }

        var chosen = useAuth ? Socks5Protocol.MethodUsernamePassword : Socks5Protocol.MethodNoAuth;
        await clientStream.WriteAsync(Socks5Protocol.BuildHandshakeReply(chosen));

        // ② 认证
        if (chosen == Socks5Protocol.MethodUsernamePassword)
        {
            var auth = await ReadAuthAsync(clientStream);
            var ok = _options.CredentialValidator?.Validate(auth.Username, auth.Password) == true;
            await clientStream.WriteAsync(Socks5Protocol.BuildAuthReply(ok));
            if (!ok)
            {
                _logger?.LogWarning("SOCKS5 代理认证失败 {Tunnel} 用户 {User}", _options.TunnelName, auth.Username);
                throw new SocksProtocolException("代理认证失败。");
            }
        }

        // ③ CONNECT 请求
        var request = await ReadConnectRequestAsync(clientStream);
        if (request.Command != Socks5Protocol.CommandConnect)
        {
            await clientStream.WriteAsync(
                Socks5Protocol.BuildConnectReply(Socks5Protocol.ReplyCommandNotSupported));
            _logger?.LogWarning("SOCKS5 不支持的命令 0x{Cmd:X2} {Tunnel} 客户端 {Client}", request.Command, _options.TunnelName, log.ClientEndpoint);
            throw new SocksProtocolException($"不支持的 SOCKS5 命令：0x{request.Command:X2}");
        }

        log.TargetEndpoint = $"{request.Host}:{request.Port}";

        if (_transport.State != TunnelState.Connected)
        {
            await clientStream.WriteAsync(
                Socks5Protocol.BuildConnectReply(Socks5Protocol.ReplyGeneralFailure));
            _logger?.LogWarning("SOCKS5 建连时隧道未连接 {Tunnel} 目标 {Target}", _options.TunnelName, log.TargetEndpoint);
            throw new IOException("SSH 隧道未连接。");
        }

        // ④ 建立隧道
        Stream channel;
        try
        {
            channel = await _transport.OpenChannelAsync(request.Host, request.Port, _cts.Token);
            await clientStream.WriteAsync(Socks5Protocol.BuildConnectReply(Socks5Protocol.ReplySucceeded));
        }
        catch (Exception ex)
        {
            await clientStream.WriteAsync(Socks5Protocol.BuildConnectReply(Socks5Protocol.MapErrorToReply(ex)));
            throw;
        }

        // ⑤ 双向透传
        var relay = await StreamRelay.RelayAsync(clientStream, channel, _options.Traffic, _cts.Token, _logger);
        log.BytesSent = relay.BytesUpstream;
        log.BytesReceived = relay.BytesDownstream;
        return channel;
    }

    private async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (n == 0)
                throw new EndOfStreamException("客户端提前断开。");
            offset += n;
        }
        return buffer;
    }

    private async Task<List<byte>> ReadHandshakeAsync(Stream stream)
    {
        var head = await ReadExactlyAsync(stream, 2, _cts.Token);
        var nMethods = head[1];
        var methodsData = await ReadExactlyAsync(stream, nMethods, _cts.Token);
        var methods = new List<byte>(nMethods);
        for (var i = 0; i < nMethods; i++)
            methods.Add(methodsData[i]);
        return methods;
    }

    private async Task<(string Username, string Password)> ReadAuthAsync(Stream stream)
    {
        var head = await ReadExactlyAsync(stream, 2, _cts.Token);
        if (head[0] != Socks5Protocol.AuthVersion)
            throw new SocksProtocolException("不支持的认证版本。");
        var ulen = head[1];
        var unameBytes = await ReadExactlyAsync(stream, ulen, _cts.Token);
        var plenByte = await ReadExactlyAsync(stream, 1, _cts.Token);
        var plen = plenByte[0];
        var passBytes = await ReadExactlyAsync(stream, plen, _cts.Token);
        return (
            Encoding.UTF8.GetString(unameBytes),
            Encoding.UTF8.GetString(passBytes));
    }

    private async Task<(byte Command, string Host, int Port)> ReadConnectRequestAsync(Stream stream)
    {
        var head = await ReadExactlyAsync(stream, 4, _cts.Token);
        if (head[0] != Socks5Protocol.Version)
            throw new SocksProtocolException("不支持的 SOCKS 版本。");

        var command = head[1];
        var atyp = head[3];

        string host;
        int port;
        switch (atyp)
        {
            case Socks5Protocol.AtypIpv4:
            {
                var addr = await ReadExactlyAsync(stream, 4, _cts.Token);
                var portBytes = await ReadExactlyAsync(stream, 2, _cts.Token);
                host = new IPAddress(addr).ToString();
                port = (portBytes[0] << 8) | portBytes[1];
                break;
            }
            case Socks5Protocol.AtypDomain:
            {
                var lenByte = await ReadExactlyAsync(stream, 1, _cts.Token);
                var len = lenByte[0];
                var hostBytes = await ReadExactlyAsync(stream, len, _cts.Token);
                var portBytes = await ReadExactlyAsync(stream, 2, _cts.Token);
                host = Encoding.UTF8.GetString(hostBytes);
                port = (portBytes[0] << 8) | portBytes[1];
                break;
            }
            case Socks5Protocol.AtypIpv6:
            {
                var addr = await ReadExactlyAsync(stream, 16, _cts.Token);
                var portBytes = await ReadExactlyAsync(stream, 2, _cts.Token);
                host = new IPAddress(addr).ToString();
                port = (portBytes[0] << 8) | portBytes[1];
                break;
            }
            default:
                throw new SocksProtocolException($"不支持的地址类型：0x{atyp:X2}");
        }

        return (command, host, port);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
