namespace NtpTool.Core.Models;

/// <summary>应用的全部配置。对应需求文档第 5.5.1 / 5.5.2 节。</summary>
public sealed class AppSettings
{
    public const string DefaultFileName = "ntp-tool-config.json";

    public NtpClientOptions Client { get; set; } = new();
    public NtpServerOptions Server { get; set; } = new();
    public LogSettings Log { get; set; } = new();

    /// <summary>返回一份深拷贝，供编辑时使用而不影响当前生效配置。</summary>
    public AppSettings Clone()
    {
        return new AppSettings
        {
            Client = Client.Clone(),
            Server = Server.Clone(),
            Log = Log.Clone()
        };
    }
}