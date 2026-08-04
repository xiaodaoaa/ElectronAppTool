using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Core.Services;

namespace NtpTool.App.Services;

/// <summary>
/// 设置窗口服务：以模态方式打开设置窗口，保存后把新配置应用到运行中的客户端/服务端服务与日志。
/// </summary>
public sealed class SettingsService
{
    private readonly IServiceProvider _services;
    private readonly IAppLogger _logger;

    public SettingsService(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<IAppLogger>();
    }

    /// <summary>
    /// 打开设置窗口。返回是否成功保存并应用了配置。
    /// </summary>
    public bool Open(Window? owner)
    {
        var repository = _services.GetRequiredService<IConfigurationRepository>();
        var settings = _services.GetRequiredService<AppSettings>();

        var vm = new SettingsViewModel(repository, settings);
        var window = new SettingsWindow(vm);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        bool? result = window.ShowDialog();
        if (result == true && window.Saved)
        {
            ApplySettings();
            return true;
        }

        return false;
    }

    private void ApplySettings()
    {
        var settings = _services.GetRequiredService<AppSettings>();
        var client = _services.GetRequiredService<INtpClientService>();
        var server = _services.GetRequiredService<INtpServerService>();
        var logger = _services.GetRequiredService<IAppLogger>();

        // 客户端：应用新配置
        client.ApplyOptions(settings.Client);

        // 服务端：应用新配置
        server.ApplyOptions(settings.Server);

        // 日志级别：动态更新
        logger.MinimumLevel = LogLevelExtensions.ParseOrDefault(settings.Log.Level);

        _logger.Information("App", "设置已应用：客户端/服务端配置与日志级别已刷新。");
    }
}