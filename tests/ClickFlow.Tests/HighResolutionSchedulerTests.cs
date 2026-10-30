using System.Diagnostics;
using ClickFlow.Engine;
using Xunit;

namespace ClickFlow.Tests;

public class HighResolutionSchedulerTests
{
    [Fact]
    public async Task WaitUntil_150ms_LandsNearTarget()
    {
        var scheduler = new HighResolutionScheduler(100);
        var ticks = 0;
        scheduler.Tick += (_, _) => ticks++;
        var target = DateTime.Now.AddMilliseconds(150);
        var sw = Stopwatch.StartNew();

        await scheduler.WaitUntilAsync(target, CancellationToken.None);
        sw.Stop();

        Assert.InRange(sw.ElapsedMilliseconds, 145, 400);
        Assert.True(ticks >= 1);
        Assert.False(scheduler.IsWaiting);
        Assert.Null(scheduler.RemainingTime);
    }

    [Fact]
    public async Task WaitUntil_FarTarget_CancelsQuickly()
    {
        var scheduler = new HighResolutionScheduler(100);
        using var cts = new CancellationTokenSource();
        var target = DateTime.Now.AddSeconds(5);

        var wait = scheduler.WaitUntilAsync(target, cts.Token);
        await Task.Delay(30);
        Assert.True(scheduler.IsWaiting);
        var sw = Stopwatch.StartNew();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds <= 50, $"cancel took {sw.ElapsedMilliseconds}ms");
        Assert.False(scheduler.IsWaiting);
    }

    [Fact]
    public async Task Delay_FineWindow_CancelsFromSpinLoop()
    {
        var scheduler = new HighResolutionScheduler(3000);
        using var cts = new CancellationTokenSource();

        var wait = scheduler.DelayAsync(2000, cts.Token);
        await Task.Delay(20);
        cts.Cancel();

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.InRange(sw.ElapsedMilliseconds, 0, 500);
    }

    [Fact]
    public async Task Delay_Measures()
    {
        var scheduler = new HighResolutionScheduler(100);
        var sw = Stopwatch.StartNew();

        await scheduler.DelayAsync(120, CancellationToken.None);
        sw.Stop();

        Assert.InRange(sw.ElapsedMilliseconds, 115, 400);
    }

    [Fact]
    public async Task Delay_ZeroOrNegative_ReturnsImmediately()
    {
        var scheduler = new HighResolutionScheduler();
        await scheduler.DelayAsync(0, CancellationToken.None);
        await scheduler.DelayAsync(-5, CancellationToken.None);
    }
}
