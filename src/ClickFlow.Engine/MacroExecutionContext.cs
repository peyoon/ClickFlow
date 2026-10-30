using ClickFlow.Core;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Engine;

public sealed class MacroExecutionContext : IExecutionContext
{
    private readonly IInputDriver _input;
    private readonly List<ExecutionLogEntry> _log = new();
    private readonly object _logLock = new();
    private readonly object _pauseLock = new();
    private TaskCompletionSource _pauseGate = CreateCompletedGate();

    public MacroExecutionContext(Macro macro, IInputDriver input, CancellationToken cancellationToken)
    {
        Macro = macro;
        _input = input;
        CancellationToken = cancellationToken;
        StartedAt = DateTime.Now;
    }

    public Macro Macro { get; }

    public DateTime StartedAt { get; }

    public CancellationToken CancellationToken { get; }

    public WindowInfo? ResolvedTargetWindow { get; set; }

    public bool IsPaused
    {
        get
        {
            lock (_pauseLock)
            {
                return !_pauseGate.Task.IsCompleted;
            }
        }
    }

    public event EventHandler<ExecutionLogEntry>? LogEmitted;

    public IReadOnlyList<ExecutionLogEntry> LogSnapshot()
    {
        lock (_logLock)
        {
            return _log.ToArray();
        }
    }

    public void Log(LogLevel level, string message, int? actionIndex = null)
    {
        var entry = new ExecutionLogEntry(DateTime.Now, level, message, actionIndex);
        lock (_logLock)
        {
            _log.Add(entry);
        }
        LogEmitted?.Invoke(this, entry);
    }

    public ScreenPoint ResolvePoint(int x, int y, CoordinateMode mode)
    {
        switch (mode)
        {
            case CoordinateMode.Window:
                if (ResolvedTargetWindow is null)
                {
                    Log(LogLevel.Warning, "대상 창이 지정되지 않아 화면 좌표로 처리합니다.");
                    return new ScreenPoint(x, y);
                }
                var client = ResolvedTargetWindow.ClientBounds;
                return new ScreenPoint(client.X + x, client.Y + y);
            case CoordinateMode.Relative:
                var cursor = _input.GetCursorPosition();
                return new ScreenPoint(cursor.X + x, cursor.Y + y);
            default:
                return new ScreenPoint(x, y);
        }
    }

    public void ThrowIfStopRequested()
    {
        if (CancellationToken.IsCancellationRequested)
        {
            throw new MacroStoppedException();
        }
    }

    public void Pause()
    {
        lock (_pauseLock)
        {
            if (!_pauseGate.Task.IsCompleted)
            {
                return;
            }
            _pauseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        lock (_pauseLock)
        {
            _pauseGate.TrySetResult();
        }
    }

    public async Task WaitIfPausedAsync()
    {
        Task gate;
        lock (_pauseLock)
        {
            gate = _pauseGate.Task;
        }

        if (gate.IsCompleted)
        {
            ThrowIfStopRequested();
            return;
        }

        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (CancellationToken.Register(() => cancelled.TrySetResult()))
        {
            await Task.WhenAny(gate, cancelled.Task).ConfigureAwait(false);
        }
        ThrowIfStopRequested();
    }

    private static TaskCompletionSource CreateCompletedGate()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }
}
