using SSHTunnelProxy.Core.Models;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 连接日志服务：SQLite 持久化，支持查询与清理。
/// </summary>
public interface ILogService
{
    /// <summary>写入一条连接日志。</summary>
    Task AddConnectionLogAsync(ConnectionLog log);

    /// <summary>
    /// 按条件查询连接日志，结果按时间倒序。
    /// </summary>
    /// <param name="tunnelName">按隧道名筛选（null 表示不限）。</param>
    /// <param name="from">起始时间（null 表示不限）。</param>
    /// <param name="to">截止时间（null 表示不限）。</param>
    /// <param name="limit">最多返回条数（null 使用服务端默认上限）。</param>
    Task<IList<ConnectionLog>> QueryLogsAsync(
        string? tunnelName = null,
        DateTime? from = null,
        DateTime? to = null,
        int? limit = null);

    /// <summary>清理超过保留天数的日志。</summary>
    Task CleanupOldLogsAsync(int retainDays);
}
