using NtpTool.Core.Models;

namespace NtpTool.Core.Services;

/// <summary>
/// 配置持久化服务：加载、保存、恢复默认。对应需求文档第 5.5 节。
/// </summary>
public interface IConfigurationRepository
{
    /// <summary>配置文件路径。</summary>
    string FilePath { get; }

    /// <summary>加载配置，文件不存在或损坏时返回默认配置。</summary>
    AppSettings Load();

    /// <summary>保存配置；保存前若失败返回 false。</summary>
    bool Save(AppSettings settings, out string? error);

    /// <summary>恢复默认配置并返回。</summary>
    AppSettings ResetToDefault();
}