using NtpTool.Core.Services;

namespace NtpTool.Core.Tests;

public class SyncSchedulerTests
{
    [Fact]
    public async Task Start_Runs_Tick()
    {
        int count = 0;
        using var scheduler = new SyncScheduler(async () =>
        {
            count++;
            await Task.CompletedTask;
        });

        scheduler.Start(TimeSpan.FromMilliseconds(20));
        // 等待定时器触发多次
        await Task.Delay(150);
        Assert.True(scheduler.IsRunning);
        Assert.True(count >= 1);
        scheduler.Stop();
    }

    [Fact]
    public async Task Stop_Prevents_More_Ticks()
    {
        int count = 0;
        using var scheduler = new SyncScheduler(async () =>
        {
            count++;
            await Task.CompletedTask;
        });

        scheduler.Start(TimeSpan.FromMilliseconds(20));
        await Task.Delay(120);
        scheduler.Stop();
        Assert.False(scheduler.IsRunning);
        int countAfterStop = count;
        await Task.Delay(120);
        Assert.Equal(countAfterStop, count);
    }
}