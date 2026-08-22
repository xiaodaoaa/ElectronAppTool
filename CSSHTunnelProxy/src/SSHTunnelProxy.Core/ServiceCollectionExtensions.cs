using Microsoft.Extensions.DependencyInjection;
using SSHTunnelProxy.Core.Proxy;
using SSHTunnelProxy.Core.Security;
using SSHTunnelProxy.Core.Services;

namespace SSHTunnelProxy.Core;

/// <summary>
/// 集中定义应用数据目录路径（与离散配置项共用）。
/// </summary>
public static class AppPaths
{
    /// <summary>应用数据根目录（程序所在文件夹）。</summary>
    public static string Root => AppContext.BaseDirectory;

    /// <summary>已信任主机密钥文件。</summary>
    public static string KnownHostsFile => Path.Combine(Root, "known_hosts.json");
}

/// <summary>
/// SSHTunnelProxy.Core 的依赖注入注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 Core 层所有服务。</summary>
    public static IServiceCollection AddSSHTunnelProxyCore(this IServiceCollection services)
    {
        services.AddSingleton<IDpapiProtector, DpapiProtector>();
        services.AddSingleton<IHostKeyVerifier>(sp =>
            new HostKeyVerifier(AppPaths.KnownHostsFile, sp.GetRequiredService<IDpapiProtector>()));
        services.AddSingleton<LogService>();
        services.AddSingleton<IConnectionSink>(sp => sp.GetRequiredService<LogService>());
        services.AddSingleton<ILogService>(sp => sp.GetRequiredService<LogService>());
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ITunnelManager, TunnelManager>();
        return services;
    }
}
