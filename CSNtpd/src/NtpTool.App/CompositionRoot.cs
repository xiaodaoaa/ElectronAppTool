using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Core.Services;
using NtpTool.Infrastructure.Config;
using NtpTool.Infrastructure.Logging;
using NtpTool.Infrastructure.Windows;

namespace NtpTool.App;

/// <summary>
/// 依赖注入注册入口。组装基础设施与领域服务，见需求文档第 7.3 节。
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider Build(AppSettings settings, string? configPath = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(settings);
        services.AddSingleton(settings.Log);
        services.AddSingleton(Dispatcher.CurrentDispatcher);

        services.AddSingleton<IAppLogger, FileLogger>();
        services.AddSingleton<IConfigurationRepository>(new JsonConfigurationRepository(configPath));
        services.AddSingleton<ISystemTimeService, WindowsSystemTimeService>();
        services.AddSingleton(settings.Client);
        services.AddSingleton(settings.Server);
        services.AddSingleton<NtpClientService>();
        services.AddSingleton<NtpServerService>();
        services.AddSingleton<INtpClientService>(sp => sp.GetRequiredService<NtpClientService>());
        services.AddSingleton<INtpServerService>(sp => sp.GetRequiredService<NtpServerService>());
        services.AddSingleton<Services.SettingsService>(sp => new Services.SettingsService(sp));
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}