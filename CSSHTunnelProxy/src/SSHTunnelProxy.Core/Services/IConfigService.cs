using SSHTunnelProxy.Core.Models;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 配置持久化服务：服务器配置 + 全局设置，敏感字段 DPAPI 加密存储。
/// </summary>
public interface IConfigService
{
    /// <summary>加载所有服务器配置。</summary>
    Task<IList<SshServerProfile>> LoadProfilesAsync();

    /// <summary>保存所有服务器配置。</summary>
    Task SaveProfilesAsync(IList<SshServerProfile> profiles);

    /// <summary>加载全局设置。</summary>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>保存全局设置。</summary>
    Task SaveSettingsAsync(AppSettings settings);
}
