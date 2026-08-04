namespace NtpTool.Core.Services;

/// <summary>
/// 定时任务调度器，用于客户端自动同步。具备防重入、可启动/停止。
/// 对应需求文档第 8.3 节。
/// </summary>
public sealed class SyncScheduler : IDisposable
{
    private readonly Func<Task> _onTick;
    private readonly TimeProvider _timeProvider;
    private SchedulePeriod _period;
    private ITimer? _timer;
    private volatile bool _running;
    private volatile bool _syncing;
    private readonly object _lock = new();

    public bool IsRunning => _running;
    public bool IsSyncing => _syncing;

    public SyncScheduler(Func<Task> onTick, TimeProvider? timeProvider = null)
    {
        _onTick = onTick;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _period = SchedulePeriod.FromMinutes(30);
    }

    /// <summary>启动定时任务。若已运行则先停止再以新周期启动。</summary>
    public void Start(TimeSpan interval)
    {
        lock (_lock)
        {
            _period = SchedulePeriod.FromTimeSpan(interval);
            StopTimerCore();
            _timer = _timeProvider.CreateTimer(_ => _ = RunTickAsync(), null, _period.DueTime, _period.RepeatInterval);
            _running = true;
        }
    }

    /// <summary>停止定时任务。</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
            StopTimerCore();
        }
    }

    private void StopTimerCore()
    {
        if (_timer is not null)
        {
            try
            {
                _timer.Dispose();
            }
            catch
            {
                // 忽略销毁期竞态
            }

            _timer = null;
        }
    }

    private async Task RunTickAsync()
    {
        if (!_running || _syncing)
        {
            return; // 防重入
        }

        _syncing = true;
        try
        {
            await _onTick().ConfigureAwait(false);
        }
        catch
        {
            // 定时器的异常不应导致进程崩溃
        }
        finally
        {
            _syncing = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private readonly struct SchedulePeriod
    {
        public TimeSpan DueTime { get; }
        public TimeSpan RepeatInterval { get; }

        private SchedulePeriod(TimeSpan dueTime, TimeSpan repeatInterval)
        {
            DueTime = dueTime;
            RepeatInterval = repeatInterval;
        }

        public static SchedulePeriod FromTimeSpan(TimeSpan interval)
        {
            return new SchedulePeriod(interval, interval);
        }

        public static SchedulePeriod FromMinutes(double minutes)
        {
            return FromTimeSpan(TimeSpan.FromMinutes(minutes));
        }
    }
}
