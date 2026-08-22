namespace SSHTunnelProxy.Core.Models;

/// <summary>
/// 全局应用设置。
/// </summary>
public class AppSettings
{
    /// <summary>是否开机自启</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>点击关闭按钮是否最小化到托盘</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>是否最小化到托盘</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>启动后是否最小化到托盘（不显示主窗口）</summary>
    public bool StartMinimizedToTray { get; set; }

    /// <summary>连接日志保留天数</summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>
    /// 上次程序退出时仍处于已连接状态的隧道配置 ID 列表。
    /// 下次启动时自动连接这些隧道。
    /// </summary>
    public List<Guid> LastConnectedProfileIds { get; set; } = new();
}
