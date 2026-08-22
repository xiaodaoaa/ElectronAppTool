using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSHTunnelProxy.App.Framework;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Services;
using System.Globalization;
using System.IO;
using System.Text;

namespace SSHTunnelProxy.App.ViewModels;

/// <summary>连接日志页 ViewModel：加载、筛选与导出日志。</summary>
public partial class LogViewModel : ObservableObject
{
    private readonly ILogService _logService;

    /// <summary>单次加载的最大条数，避免日志过多时 DataGrid 渲染大量行导致卡顿。</summary>
    private const int MaxDisplayCount = 500;

    /// <summary>
    /// 使用批量集合：加载时整体替换并只触发一次 Reset 通知，
    /// 而非逐条 Add 触发 N 次集合变更（DataGrid 会反复重排布局而卡顿）。
    /// </summary>
    public RangeObservableCollection<ConnectionLog> Logs { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public LogViewModel(ILogService logService)
    {
        _logService = logService;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            // 直接在 SQL 层限制返回条数，避免日志量大时一次性拉取数万行到内存。
            var logs = await _logService.QueryLogsAsync(limit: MaxDisplayCount);
            var display = logs.ToArray();

            Logs.ReplaceAll(display);
            StatusMessage = $"共 {display.Length} 条记录。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"proxy-logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("时间,隧道,代理类型,客户端,目标,上传字节,下载字节,持续时间,状态");
            foreach (var log in Logs)
            {
                sb.AppendLine(string.Join(',',
                    log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    CsvEscape(log.TunnelName),
                    log.ProxyType,
                    CsvEscape(log.ClientEndpoint),
                    CsvEscape(log.TargetEndpoint),
                    log.BytesSent,
                    log.BytesReceived,
                    log.Duration.TotalSeconds.ToString("0.0"),
                    log.Status));
            }
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"已导出到 {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败：{ex.Message}";
        }
    }

    private static string CsvEscape(string value)
        => value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
