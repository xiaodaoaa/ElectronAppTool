using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NtpTool.Core.Models;

/// <summary>单个上游 NTP 服务器配置。见需求文档第 9.1 节。</summary>
public sealed class NtpServerConfig : INotifyPropertyChanged
{
    private string _host = string.Empty;
    private int _port = 123;
    private int _priority = 100;
    private bool _enabled = true;
    private string? _remark;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public int Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string? Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}