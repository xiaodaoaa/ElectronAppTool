using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSHTunnelProxy.App.Framework;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Services;

namespace SSHTunnelProxy.App.ViewModels;

/// <summary>全局设置页 ViewModel。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _config;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _startMinimizedToTray;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private int _logRetentionDays = 30;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(IConfigService config)
    {
        _config = config;
        Load();
    }

    private void Load()
    {
        var settings = _config.LoadSettingsAsync().GetAwaiter().GetResult();
        CloseToTray = settings.CloseToTray;
        MinimizeToTray = settings.MinimizeToTray;
        StartMinimizedToTray = settings.StartMinimizedToTray;
        // 以注册表实际状态为准，而非仅信任配置文件（可能被外部改动）。
        StartWithWindows = StartupRegistrar.IsRegistered();
        LogRetentionDays = settings.LogRetentionDays;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // 同步注册表：勾选则写入，取消则删除。失败提示用户但不阻断其余设置保存。
        try
        {
            if (StartWithWindows)
                StartupRegistrar.Register();
            else
                StartupRegistrar.Unregister();
        }
        catch (Exception ex)
        {
            StatusMessage = $"开机自启设置失败：{ex.Message}";
            // 回滚 UI 状态为注册表实际值，避免复选框与系统状态不一致。
            StartWithWindows = StartupRegistrar.IsRegistered();
            return;
        }

        var settings = new AppSettings
        {
            CloseToTray = CloseToTray,
            MinimizeToTray = MinimizeToTray,
            StartMinimizedToTray = StartMinimizedToTray,
            StartWithWindows = StartWithWindows,
            LogRetentionDays = LogRetentionDays,
        };

        await _config.SaveSettingsAsync(settings);
        StatusMessage = "设置已保存。";
    }
}
