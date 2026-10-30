using System.Collections.ObjectModel;
using System.Windows;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClickFlow.App.Services;

public sealed partial class ExecutionService : ObservableObject
{
    private readonly IMacroExecutor _executor;
    private readonly IScheduler _scheduler;
    private CancellationTokenSource? _cts;

    public ExecutionService(IMacroExecutor executor, IScheduler scheduler)
    {
        _executor = executor;
        _scheduler = scheduler;
        _executor.StateChanged += (_, s) => Dispatch(() => ApplyState(s));
        _executor.LogEmitted += (_, e) => Dispatch(() => AppendLog(e));
        _executor.ActionStarted += (_, i) => Dispatch(() => CurrentActionIndex = i);
        _scheduler.Tick += (_, t) => Dispatch(() => Remaining = t);
    }

    public ObservableCollection<ExecutionLogEntry> Log { get; } = new();
    public ObservableCollection<ExecutionResult> History { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    [NotifyPropertyChangedFor(nameof(CanResume))]
    private ExecutionState _state = ExecutionState.Idle;

    [ObservableProperty]
    private Macro? _currentMacro;

    [ObservableProperty]
    private int _currentActionIndex = -1;

    [ObservableProperty]
    private TimeSpan? _remaining;

    [ObservableProperty]
    private ExecutionResult? _lastResult;

    public bool IsBusy => State is ExecutionState.Preparing or ExecutionState.Waiting or ExecutionState.Running or ExecutionState.Paused;
    public bool CanPause => State is ExecutionState.Running or ExecutionState.Waiting;
    public bool CanResume => State is ExecutionState.Paused;

    public event EventHandler<ExecutionResult>? RunFinished;

    public async Task<ExecutionResult> RunAsync(Macro macro)
    {
        if (IsBusy) throw new InvalidOperationException("이미 실행 중인 매크로가 있습니다.");

        _cts = new CancellationTokenSource();
        CurrentMacro = macro;
        CurrentActionIndex = -1;
        Remaining = null;
        Log.Clear();

        try
        {
            var result = await _executor.RunAsync(macro, _cts.Token);
            LastResult = result;
            History.Insert(0, result);
            if (History.Count > 50) History.RemoveAt(History.Count - 1);
            RunFinished?.Invoke(this, result);
            return result;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            CurrentActionIndex = -1;
            Remaining = null;
        }
    }

    public void Pause() => _executor.Pause();
    public void Resume() => _executor.Resume();

    public void Stop()
    {
        _executor.Stop();
        _cts?.Cancel();
    }

    public void TogglePause()
    {
        if (CanPause) Pause();
        else if (CanResume) Resume();
    }

    private void ApplyState(ExecutionState state)
    {
        State = state;
        if (state is ExecutionState.Paused) Remaining = null;
    }

    private void AppendLog(ExecutionLogEntry entry)
    {
        Log.Add(entry);
        if (Log.Count > 2000) Log.RemoveAt(0);
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
