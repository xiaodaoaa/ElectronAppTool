using System.Net.Sockets;
using System.Threading;
using NtpTool.Core.Models;
using NtpTool.Core.Ntp;
using NtpTool.Core.Services;

namespace NtpTool.Core.Tests;

/// <summary>
/// 服务端集成测试：启动服务端于高端口，使用真实 UDP 客户端请求并校验响应。
/// 对应需求文档第 16.2 节。
/// </summary>
public class NtpServerIntegrationTests
{
    private sealed class NullLogger : Core.Logging.IAppLogger
    {
        public Core.Logging.LogLevel MinimumLevel { get; set; } = Core.Logging.LogLevel.Trace;
#pragma warning disable CS0067
        public event EventHandler<Core.Logging.LogEntry>? EntryWritten;
#pragma warning restore CS0067
        public void Log(Core.Logging.LogLevel level, string module, string message, Exception? exception = null) { }
        public void Dispose() { }
    }

    [Fact]
    public async Task Server_Responds_To_Client_Request()
    {
        int port = GetFreePort();
        var options = new NtpServerOptions
        {
            EnableServer = true,
            ListenAddress = "127.0.0.1",
            Port = port,
            Stratum = 2,
            ReferenceId = "LOCAL",
            AllowAllClients = true
        };

        using var server = new NtpServerService(options, new NullLogger());
        await server.StartAsync();
        try
        {
            Assert.Equal(ServerState.Listening, server.State);

            using var client = new UdpClient();
            client.Connect("127.0.0.1", port);
            byte[] request = NtpPacketCodec.Encode(NtpPacket.CreateClientRequest(DateTime.UtcNow));
            await client.SendAsync(request, request.Length);
            await Task.Delay(100);
            var responseTask = client.ReceiveAsync();
            var received = await responseTask;
            NtpPacket response = NtpPacketCodec.Decode(received.Buffer);

            Assert.Equal((byte)NtpMode.Server, response.Mode);
            Assert.Equal((byte)2, response.Stratum);
            Assert.NotEqual(NtpTime.Zero(), response.TransmitTimestamp);

            // 统计应更新
            Assert.True(server.Statistics.TotalRequests >= 1);
            Assert.True(server.Statistics.ValidRequests >= 1);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task Server_Rejects_Whitelisted_Blocked_Client()
    {
        int port = GetFreePort();
        var options = new NtpServerOptions
        {
            EnableServer = true,
            ListenAddress = "127.0.0.1",
            Port = port,
            AllowAllClients = false,
            AllowedNetworks = new List<string> { "192.168.99.0/24" }
        };

        using var server = new NtpServerService(options, new NullLogger());
        await server.StartAsync();
        try
        {
            using var client = new UdpClient();
            client.Connect("127.0.0.1", port);
            byte[] request = NtpPacketCodec.Encode(NtpPacket.CreateClientRequest(DateTime.UtcNow));
            await client.SendAsync(request, request.Length);
            await Task.Delay(200);
            Assert.True(server.Statistics.RejectedRequests >= 1);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task Server_Binds_Fail_Reports_Error_On_Occupied_Port()
    {
        // 先占用一个端口，再尝试启动服务端应进入 Error 状态
        using var blocker = new UdpClient();
        blocker.ExclusiveAddressUse = false;
        int port = GetFreePort();
        blocker.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port));

        var options = new NtpServerOptions { ListenAddress = "127.0.0.1", Port = port };
        using var server = new NtpServerService(options, new NullLogger());
        await server.StartAsync();
        try
        {
            Assert.Equal(ServerState.Error, server.State);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task StatisticsChanged_Event_Fires_On_Request()
    {
        int port = GetFreePort();
        var options = new NtpServerOptions
        {
            EnableServer = true,
            ListenAddress = "127.0.0.1",
            Port = port,
            AllowAllClients = true
        };

        using var server = new NtpServerService(options, new NullLogger());
        int eventCount = 0;
        server.StatisticsChanged += (_, _) => Interlocked.Increment(ref eventCount);
        await server.StartAsync();
        try
        {
            using var client = new UdpClient();
            client.Connect("127.0.0.1", port);
            byte[] request = NtpPacketCodec.Encode(NtpPacket.CreateClientRequest(DateTime.UtcNow));
            await client.SendAsync(request, request.Length);
            var received = await client.ReceiveAsync();
            Assert.Equal(NtpPacketCodec.PacketSize, received.Buffer.Length);

            // 事件应实时触发，无需停止服务
            await SpinUntilAsync(() => eventCount >= 1, TimeSpan.FromSeconds(2));
            Assert.True(eventCount >= 1, "StatisticsChanged 事件未在收到请求时触发。");
            Assert.True(server.Statistics.TotalRequests >= 1);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    private static async Task SpinUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private static int GetFreePort()
    {
        using var udp = new UdpClient(0);
        return ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }
}