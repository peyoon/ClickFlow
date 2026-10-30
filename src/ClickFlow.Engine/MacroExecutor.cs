using ClickFlow.Core;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Engine;

public sealed class MacroExecutor : IMacroExecutor
{
    private static readonly TimeSpan ApproachingThreshold = TimeSpan.FromSeconds(1);

    private readonly ActionExecutor _actions;
    private readonly IScheduler _scheduler;
    private readonly IWindowService _windows;
    private readonly IInputDriver _input;
    private readonly object _sync = new();

    private ExecutionState _state = ExecutionState.Idle;
    private CancellationTokenSource? _cts;
    private MacroExecutionContext? _context;
    private int _executedActionCount;

    public MacroExecutor(ActionExecutor actions, IScheduler scheduler, IWindowService windows, IInputDriver input)
    {
        _actions = actions;
        _scheduler = scheduler;
        _windows = windows;
        _input = input;
    }

    public ExecutionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public event EventHandler<ExecutionState>? StateChanged;
    public event EventHandler<ExecutionLogEntry>? LogEmitted;
    public event EventHandler<int>? ActionStarted;

    public async Task<ExecutionResult> RunAsync(Macro macro, CancellationToken ct = default)
    {
        CancellationTokenSource cts;
        MacroExecutionContext ctx;
        lock (_sync)
        {
            if (IsActive(_state))
            {
                throw new InvalidOperationException($"매크로가 이미 실행 중입니다. (상태: {_state})");
            }
            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ctx = new MacroExecutionContext(macro, _input, cts.Token);
            _cts = cts;
            _context = ctx;
            _executedActionCount = 0;
        }

        ctx.LogEmitted += OnContextLog;
        SetState(ExecutionState.Preparing);

        var finalState = ExecutionState.Completed;
        string? errorMessage = null;
        ScreenPoint? originalCursor = null;

        try
        {
            originalCursor = Prepare(ctx);
            await RunScheduledAsync(ctx).ConfigureAwait(false);
            ctx.Log(LogLevel.Info, "매크로 완료");
        }
        catch (MacroStoppedException)
        {
            finalState = ExecutionState.Stopped;
            ctx.Log(LogLevel.Info, "사용자 중지");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            finalState = ExecutionState.Stopped;
            ctx.Log(LogLevel.Info, "사용자 중지");
        }
        catch (MacroCompletedException ex)
        {
            if (ex.Success)
            {
                ctx.Log(LogLevel.Info, "매크로 완료 (종료 동작)");
            }
            else
            {
                finalState = ExecutionState.Error;
                errorMessage = ex.Message;
                ctx.Log(LogLevel.Error, ex.Message);
            }
        }
        catch (MacroAbortedException ex)
        {
            finalState = ExecutionState.Error;
            errorMessage = ex.Message;
            ctx.Log(LogLevel.Error, $"매크로 중단: {ex.Message}");
        }
        catch (Exception ex)
        {
            finalState = ExecutionState.Error;
            errorMessage = ex.Message;
            ctx.Log(LogLevel.Error, $"오류로 종료: {ex.Message}");
        }
        finally
        {
            RestoreCursor(ctx, originalCursor);
            ctx.LogEmitted -= OnContextLog;
            lock (_sync)
            {
                _cts = null;
                _context = null;
            }
            cts.Dispose();
        }

        SetState(finalState);

        return new ExecutionResult
        {
            MacroId = macro.Id,
            MacroName = macro.Name,
            FinalState = finalState,
            StartedAt = ctx.StartedAt,
            EndedAt = DateTime.Now,
            ExecutedActionCount = Volatile.Read(ref _executedActionCount),
            ErrorMessage = errorMessage,
            Log = ctx.LogSnapshot(),
        };
    }

    public void Pause()
    {
        MacroExecutionContext? ctx;
        lock (_sync)
        {
            if (_state != ExecutionState.Running || _context is null)
            {
                return;
            }
            ctx = _context;
            ctx.Pause();
        }
        ctx.Log(LogLevel.Info, "일시 정지");
        SetState(ExecutionState.Paused);
    }

    public void Resume()
    {
        MacroExecutionContext? ctx;
        lock (_sync)
        {
            if (_state != ExecutionState.Paused || _context is null)
            {
                return;
            }
            ctx = _context;
            ctx.Resume();
        }
        ctx.Log(LogLevel.Info, "재개");
        SetState(ExecutionState.Running);
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            if (!IsActive(_state))
            {
                return;
            }
            cts = _cts;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private ScreenPoint? Prepare(MacroExecutionContext ctx)
    {
        var macro = ctx.Macro;
        ctx.Log(LogLevel.Info, $"매크로 준비: {macro.Name}");

        if (macro.TargetWindow.IsSpecified)
        {
            var window = _windows.FindWindow(macro.TargetWindow.TitleContains, macro.TargetWindow.ProcessName);
            if (window is null)
            {
                ctx.Log(LogLevel.Warning, $"대상 창을 찾지 못했습니다: {macro.TargetWindow}");
            }
            else
            {
                ctx.ResolvedTargetWindow = window;
                ctx.Log(LogLevel.Info, $"대상 창: '{window.Title}' ({window.ProcessName})");
                if (macro.TargetWindow.BringToFront && !_windows.BringToFront(window.Handle))
                {
                    ctx.Log(LogLevel.Warning, "대상 창을 앞으로 가져오지 못했습니다.");
                }
            }
        }

        return macro.Settings.RestoreCursorAfterRun ? _input.GetCursorPosition() : null;
    }

    private void RestoreCursor(MacroExecutionContext ctx, ScreenPoint? original)
    {
        if (original is null)
        {
            return;
        }

        try
        {
            _input.MoveTo(original.X, original.Y);
            ctx.Log(LogLevel.Debug, $"커서 위치 복원: {original.X}, {original.Y}");
        }
        catch (Exception ex)
        {
            ctx.Log(LogLevel.Warning, $"커서 위치 복원 실패: {ex.Message}");
        }
    }

    private async Task RunScheduledAsync(MacroExecutionContext ctx)
    {
        var schedule = ctx.Macro.Schedule;
        switch (schedule.Type)
        {
            case ScheduleType.Once:
                await WaitForStartTimeAsync(ctx, schedule.StartTime).ConfigureAwait(false);
                SetState(ExecutionState.Running);
                await RunPassWithRetriesAsync(ctx).ConfigureAwait(false);
                break;
            case ScheduleType.Interval:
                await RunIntervalAsync(ctx, schedule).ConfigureAwait(false);
                break;
            default:
                SetState(ExecutionState.Running);
                await RunPassWithRetriesAsync(ctx).ConfigureAwait(false);
                break;
        }
    }

    private async Task WaitForStartTimeAsync(MacroExecutionContext ctx, DateTime? startTime)
    {
        if (startTime is null || startTime.Value <= DateTime.Now)
        {
            if (startTime is not null)
            {
                ctx.Log(LogLevel.Warning, $"예약 시각이 이미 지나 즉시 실행합니다: {startTime:HH:mm:ss.fff}");
            }
            return;
        }

        var target = startTime.Value;
        SetState(ExecutionState.Waiting);
        ctx.Log(LogLevel.Info, $"스케줄러 시작 · 목표 {target:yyyy-MM-dd HH:mm:ss.fff}");

        var approachingLogged = false;
        void OnTick(object? sender, TimeSpan remaining)
        {
            if (!approachingLogged && remaining <= ApproachingThreshold && remaining > TimeSpan.Zero)
            {
                approachingLogged = true;
                ctx.Log(LogLevel.Info, $"목표 시각 접근 (남은 시간 {remaining.TotalMilliseconds:0} ms)");
            }
        }

        _scheduler.Tick += OnTick;
        try
        {
            await _scheduler.WaitUntilAsync(target, ctx.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scheduler.Tick -= OnTick;
        }

        var fired = DateTime.Now;
        var drift = (fired - target).TotalMilliseconds;
        ctx.Log(LogLevel.Info, $"목표 시각 도달 {fired:HH:mm:ss.fff} (오차 {drift:+0.0;-0.0}ms)");
    }

    private async Task RunIntervalAsync(MacroExecutionContext ctx, Schedule schedule)
    {
        var infinite = schedule.RepeatCount <= 0;
        for (var pass = 1; infinite || pass <= schedule.RepeatCount; pass++)
        {
            if (pass > 1)
            {
                SetState(ExecutionState.Waiting);
                ctx.Log(LogLevel.Info, $"다음 실행까지 {schedule.IntervalMs} ms 대기");
                await _scheduler.DelayAsync(schedule.IntervalMs, ctx.CancellationToken).ConfigureAwait(false);
            }

            SetState(ExecutionState.Running);
            ctx.Log(LogLevel.Info, infinite ? $"실행 {pass}회차" : $"실행 {pass}/{schedule.RepeatCount}회차");
            await RunPassWithRetriesAsync(ctx).ConfigureAwait(false);
        }
    }

    private async Task RunPassWithRetriesAsync(MacroExecutionContext ctx)
    {
        var settings = ctx.Macro.Settings;
        var maxRetries = Math.Max(0, settings.MaxRetries);
        var attempt = 0;

        while (true)
        {
            try
            {
                await RunPassAsync(ctx, allowRetry: attempt < maxRetries).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsOrdinaryFailure(ex) && attempt < maxRetries)
            {
                attempt++;
                ctx.Log(LogLevel.Warning, $"오류 발생, 재시도 {attempt}/{maxRetries}: {ex.Message}");
                await _scheduler.DelayAsync(settings.RetryIntervalMs, ctx.CancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunPassAsync(MacroExecutionContext ctx, bool allowRetry)
    {
        var macro = ctx.Macro;
        var settings = macro.Settings;

        for (var index = 0; index < macro.Actions.Count; index++)
        {
            await ctx.WaitIfPausedAsync().ConfigureAwait(false);
            ctx.ThrowIfStopRequested();

            if (index > 0 && settings.DefaultDelayBetweenActionsMs > 0)
            {
                await _scheduler.DelayAsync(settings.DefaultDelayBetweenActionsMs, ctx.CancellationToken).ConfigureAwait(false);
                await ctx.WaitIfPausedAsync().ConfigureAwait(false);
            }

            ActionStarted?.Invoke(this, index);

            try
            {
                await _actions.ExecuteAsync(macro.Actions[index], ctx, index).ConfigureAwait(false);
                Interlocked.Increment(ref _executedActionCount);
            }
            catch (Exception ex) when (IsOrdinaryFailure(ex) && !allowRetry && !settings.StopOnError)
            {
                ctx.Log(LogLevel.Error, $"동작 #{index + 1} 실패, 계속 진행: {ex.Message}", index);
            }
        }
    }

    private void OnContextLog(object? sender, ExecutionLogEntry entry)
    {
        LogEmitted?.Invoke(this, entry);
    }

    private void SetState(ExecutionState state)
    {
        lock (_sync)
        {
            if (_state == state)
            {
                return;
            }
            _state = state;
        }
        StateChanged?.Invoke(this, state);
    }

    private static bool IsActive(ExecutionState state) =>
        state is ExecutionState.Preparing or ExecutionState.Waiting or ExecutionState.Running or ExecutionState.Paused;

    private static bool IsOrdinaryFailure(Exception ex) =>
        ex is not (OperationCanceledException or MacroCompletedException or MacroAbortedException);
}
