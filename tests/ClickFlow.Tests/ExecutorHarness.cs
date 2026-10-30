using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Engine;
using ClickFlow.Tests.Fakes;

namespace ClickFlow.Tests;

internal sealed class ExecutorHarness
{
    public FakeInputDriver Input { get; } = new();

    public FakeScreenService Screen { get; } = new();

    public FakeWindowService Windows { get; } = new();

    public IScheduler Scheduler { get; }

    public MacroExecutor Executor { get; }

    public List<ExecutionState> States { get; } = new();

    public List<int> StartedActions { get; } = new();

    public List<ExecutionLogEntry> Logs { get; } = new();

    public ExecutorHarness(IScheduler? scheduler = null)
    {
        Scheduler = scheduler ?? new ImmediateScheduler();
        var conditions = new ConditionEngine(Screen, Windows);
        var actions = new ActionExecutor(Input, Screen, Scheduler, conditions, () => Path.GetTempPath());
        Executor = new MacroExecutor(actions, Scheduler, Windows, Input);
        Executor.StateChanged += (_, s) => { lock (States) States.Add(s); };
        Executor.ActionStarted += (_, i) => { lock (StartedActions) StartedActions.Add(i); };
        Executor.LogEmitted += (_, e) => { lock (Logs) Logs.Add(e); };
    }

    public static Macro MacroWith(params ActionStep[] actions) => new()
    {
        Name = "test",
        Actions = actions.ToList(),
    };
}
