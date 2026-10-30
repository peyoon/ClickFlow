using System.Diagnostics;
using ClickFlow.Core.Interfaces;

namespace ClickFlow.Engine;

public sealed class HighResolutionScheduler : IScheduler
{
    private const int CoarseMarginMs = 20;
    private const int MaxCoarseSliceMs = 250;

    private readonly int _highResolutionWindowMs;
    private TimeSpan? _remaining;
    private volatile bool _isWaiting;

    public HighResolutionScheduler(int highResolutionWindowMs = 100)
    {
        _highResolutionWindowMs = Math.Max(1, highResolutionWindowMs);
    }

    public TimeSpan? RemainingTime => _remaining;

    public bool IsWaiting => _isWaiting;

    public event EventHandler<TimeSpan>? Tick;

    // Two-phase wait: a coarse phase uses Task.Delay (cheap, ~15ms granularity on Windows)
    // until the target is within the high-resolution window, then a fine phase spins on a
    // thread-pool thread using Stopwatch ticks so the final approach lands within ~1ms.
    public async Task WaitUntilAsync(DateTime target, CancellationToken ct)
    {
        _isWaiting = true;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var remaining = target - DateTime.Now;
                _remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
                RaiseTick(_remaining.Value);

                var remainingMs = remaining.TotalMilliseconds;
                if (remainingMs <= _highResolutionWindowMs + CoarseMarginMs)
                {
                    break;
                }

                var sliceMs = Math.Min(remainingMs - _highResolutionWindowMs, MaxCoarseSliceMs);
                await Task.Delay(TimeSpan.FromMilliseconds(sliceMs), ct).ConfigureAwait(false);
            }

            var fineRemaining = target - DateTime.Now;
            if (fineRemaining > TimeSpan.Zero)
            {
                await Task.Run(() => SpinUntil(fineRemaining, ct), ct).ConfigureAwait(false);
            }

            _remaining = TimeSpan.Zero;
            RaiseTick(TimeSpan.Zero);
        }
        finally
        {
            _isWaiting = false;
            _remaining = null;
        }
    }

    public async Task DelayAsync(int milliseconds, CancellationToken ct)
    {
        if (milliseconds <= 0)
        {
            ct.ThrowIfCancellationRequested();
            return;
        }

        var sw = Stopwatch.StartNew();
        if (milliseconds > _highResolutionWindowMs)
        {
            await Task.Delay(milliseconds - _highResolutionWindowMs, ct).ConfigureAwait(false);
        }

        var fineRemaining = TimeSpan.FromMilliseconds(milliseconds) - sw.Elapsed;
        if (fineRemaining > TimeSpan.Zero)
        {
            await Task.Run(() => SpinUntil(fineRemaining, ct), ct).ConfigureAwait(false);
        }
    }

    private static void SpinUntil(TimeSpan duration, CancellationToken ct)
    {
        var targetTicks = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        var spinner = new SpinWait();
        while (Stopwatch.GetTimestamp() < targetTicks)
        {
            ct.ThrowIfCancellationRequested();
            var leftTicks = targetTicks - Stopwatch.GetTimestamp();
            if (leftTicks > Stopwatch.Frequency / 500)
            {
                Thread.Yield();
            }
            else
            {
                spinner.SpinOnce(-1);
            }
        }
    }

    private void RaiseTick(TimeSpan remaining)
    {
        Tick?.Invoke(this, remaining);
    }
}
