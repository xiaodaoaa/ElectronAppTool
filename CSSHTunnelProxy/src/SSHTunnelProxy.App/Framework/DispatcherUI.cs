using System.Windows;
using System.Windows.Threading;

namespace SSHTunnelProxy.App.Framework;

/// <summary>
/// UI 线程调度助手：将回调封送到 UI 线程，供非 UI 线程的事件处理器安全更新绑定属性。
/// </summary>
public sealed class DispatcherUI
{
    private readonly Dispatcher _dispatcher;

    public DispatcherUI()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>在 UI 线程上异步执行操作（已在 UI 线程则直接执行）。</summary>
    public void Run(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    /// <summary>在 UI 线程上同步执行操作。</summary>
    public T Invoke<T>(Func<T> func)
    {
        if (_dispatcher.CheckAccess())
            return func();
        return _dispatcher.Invoke(func);
    }
}
