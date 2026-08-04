namespace NtpTool.Core.Models;

/// <summary>NTP 服务端配置。见需求文档第 9.3 节。</summary>
public sealed class NtpServerOptions
{
    public bool EnableServer { get; set; }
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 123;
    public byte Stratum { get; set; } = 2;
    public string ReferenceId { get; set; } = "LOCAL";
    public bool AllowAllClients { get; set; } = true;
    public byte LeapIndicator { get; set; }
    public byte Poll { get; set; } = 6;
    public sbyte Precision { get; set; } = -6;
    public uint RootDelay { get; set; } = 0x00010000;
    public uint RootDispersion { get; set; } = 0x00010000;
    public List<string> AllowedNetworks { get; set; } = new();
    public int RateLimitPerMinute { get; set; } = 120;
    public bool LogRequests { get; set; } = true;
    public bool LogRejectedRequests { get; set; } = true;

    public NtpServerOptions Clone()
    {
        return new NtpServerOptions
        {
            EnableServer = EnableServer,
            ListenAddress = ListenAddress,
            Port = Port,
            Stratum = Stratum,
            ReferenceId = ReferenceId,
            AllowAllClients = AllowAllClients,
            LeapIndicator = LeapIndicator,
            Poll = Poll,
            Precision = Precision,
            RootDelay = RootDelay,
            RootDispersion = RootDispersion,
            AllowedNetworks = new List<string>(AllowedNetworks),
            RateLimitPerMinute = RateLimitPerMinute,
            LogRequests = LogRequests,
            LogRejectedRequests = LogRejectedRequests
        };
    }
}