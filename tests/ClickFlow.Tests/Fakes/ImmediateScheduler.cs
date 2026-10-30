using ClickFlow.Core.Interfaces;

namespace ClickFlow.Tests.Fakes;

public sealed class ImmediateScheduler : IScheduler
{
    public List<int> RequestedDelays { get; } = new();

    public List<DateTime> RequestedTargets { get; } = new();

    public TimeSpan? RemainingTime => null;

    public bool IsWaiting => false;

    public event EventHandler<TimeSpan>? Tick;

    public Task WaitUntilAsync(DateTime target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (RequestedTargets)
        {
            RequestedTargets.Add(target);
        }
        Tick?.Invoke(this, TimeSpan.Zero);
        return Task.CompletedTask;
    }

    public Task DelayAsync(int milliseconds, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (RequestedDelays)
        {
            RequestedDelays.Add(milliseconds);
        }
        return Task.CompletedTask;
    }
}

public sealed class RealDelayScheduler : IScheduler
{
    public TimeSpan? RemainingTime => null;

    public bool IsWaiting => false;

    public event EventHandler<TimeSpan>? Tick;

    public async Task WaitUntilAsync(DateTime target, CancellationToken ct)
    {
        var remaining = target - DateTime.Now;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, ct);
        }
        Tick?.Invoke(this, TimeSpan.Zero);
    }

    public Task DelayAsync(int milliseconds, CancellationToken ct)
    {
        return milliseconds <= 0 ? Task.CompletedTask : Task.Delay(milliseconds, ct);
    }
}
