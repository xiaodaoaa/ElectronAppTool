using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.Core.Services;

/// <summary>
/// 系统时间应用策略，决定同步后是否修改本地系统时间。
/// 对应需求文档第 5.2.6 节（仅显示 / 提示确认 / 自动修改 / 仅管理员修改）。
/// </summary>
public interface ITimeApplyingStrategy
{
    /// <summary>根据结果与配置决定是否修改系统时间，返回是否实际修改。</summary>
    bool TryApply(NtpSyncResult result, NtpClientOptions options);
}

/// <summary>
/// 默认策略实现：仅在开启 <see cref="NtpClientOptions.ApplySystemTime"/> 且偏差满足阈值时调用系统时间服务，
/// 并且要求进程具备管理员权限。
/// </summary>
public sealed class DefaultTimeApplyingStrategy : ITimeApplyingStrategy
{
    private readonly ISystemTimeService _systemTime;
    private readonly IAppLogger _logger;

    public DefaultTimeApplyingStrategy(ISystemTimeService systemTime, IAppLogger logger)
    {
        _systemTime = systemTime;
        _logger = logger;
    }

    public bool TryApply(NtpSyncResult result, NtpClientOptions options)
    {
        if (!options.ApplySystemTime)
        {
            return false;
        }

        double offset = Math.Abs(result.OffsetMs);
        if (offset > options.MaxAllowedOffsetMs)
        {
            _logger.Warning("SystemTime",
                $"检测到时间偏差过大（{offset:0}ms），超出最大允许偏差（{options.MaxAllowedOffsetMs:0}ms），不自动修改。");
            return false;
        }

        if (offset < options.AutoApplyThresholdMs)
        {
            return false;
        }

        if (!_systemTime.IsAdministrator())
        {
            _logger.Warning("SystemTime", "非管理员权限，无法修改系统时间。");
            return false;
        }

        if (_systemTime.IsWindowsTimeServiceRunning())
        {
            _logger.Warning("SystemTime", "检测到 Windows Time 服务正在运行，自动修改的系统时间可能被系统服务覆盖。");
        }

        // 平滑计算目标本地时间
        DateTime targetLocal = _systemTime.GetLocalNow().AddMilliseconds(result.OffsetMs);
        DateTime before = _systemTime.GetLocalNow();
        try
        {
            _systemTime.SetLocalTime(targetLocal);
            result.SystemTimeChanged = true;
            _logger.Information("SystemTime",
                $"修改系统时间成功：{before:yyyy-MM-dd HH:mm:ss.fff} → {targetLocal:yyyy-MM-dd HH:mm:ss.fff}。");
            return true;
        }
        catch (SystemTimeException ex)
        {
            _logger.Error("SystemTime", $"设置系统时间失败：{ex.Message}", ex);
            return false;
        }
    }
}