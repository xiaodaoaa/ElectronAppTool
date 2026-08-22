using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SSHTunnelProxy.App.Framework;

/// <summary>
/// 支持批量增删的 ObservableCollection：绕过逐条通知，操作完成后只触发一次 Reset 通知。
/// 避免一次性加载大量日志时 ObservableCollection 逐条 Add 触发 N 次集合变更，
/// 导致 DataGrid 反复重排布局而卡顿。
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>清空并以指定集合整体替换，仅触发一次 Reset 通知。</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
