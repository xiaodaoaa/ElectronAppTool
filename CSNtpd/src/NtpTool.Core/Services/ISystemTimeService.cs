namespace NtpTool.Core.Services;

/// <summary>
/// 系统时间服务：获取本地/UTC 时间，以及在管理员权限下设置系统时间。
/// 对应需求文档第 5.4 节。
/// </summary>
public interface ISystemTimeService
{
    /// <summary>获取本地时间。</summary>
    DateTime GetLocalNow();

    /// <summary>获取 UTC 时间。</summary>
    DateTime GetUtcNow();

    /// <summary>当前进程是否具备管理员权限。</summary>
    bool IsAdministrator();

    /// <summary>
    /// 尝试设置系统时间。非管理员或修改失败时抛出异常。
    /// </summary>
    void SetLocalTime(DateTime localTime);

    /// <summary>Windows Time (w32time) 服务是否正在运行。</summary>
    bool IsWindowsTimeServiceRunning();
}

/// <summary>设置系统时间失败时抛出的异常。</summary>
public sealed class SystemTimeException : Exception
{
    public SystemTimeException(string message) : base(message) { }
    public SystemTimeException(string message, Exception inner) : base(message, inner) { }
}