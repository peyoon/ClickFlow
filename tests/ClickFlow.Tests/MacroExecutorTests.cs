using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Tests.Fakes;
using Xunit;

namespace ClickFlow.Tests;

public class MacroExecutorTests
{
    [Fact]
    public async Task RunsActionsInOrder_AndCompletes()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = 1200, Y = 700 },
            new WaitAction { Milliseconds = 300 },
            new MouseClickAction { X = 1100, Y = 650 },
            new KeyPressAction { Key = "ENTER" });

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(ExecutionState.Completed, h.Executor.State);
        Assert.Equal(4, result.ExecutedActionCount);
        Assert.Equal(
            new[] { "Click(1200,700,Left,1)", "Click(1100,650,Left,1)", "KeyPress(ENTER,None)" },
            h.Input.Log);
        Assert.Contains(300, ((ImmediateScheduler)h.Scheduler).RequestedDelays);
        Assert.Equal(
            new[] { ExecutionState.Preparing, ExecutionState.Running, ExecutionState.Completed },
            h.States);
        Assert.Equal(new[] { 0, 1, 2, 3 }, h.StartedActions);
        Assert.Contains(result.Log, e => e.Message.Contains("매크로 완료"));
    }

    [Fact]
    public async Task Stop_DuringInfiniteRepeat_EndsStopped()
    {
        var h = new ExecutorHarness(new RealDelayScheduler());
        var macro = ExecutorHarness.MacroWith(
            new RepeatAction { Count = 0, Actions = { new WaitAction { Milliseconds = 20 } } });

        var run = h.Executor.RunAsync(macro);
        await Task.Delay(100);
        Assert.Equal(ExecutionState.Running, h.Executor.State);
        h.Executor.Stop();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ExecutionState.Stopped, result.FinalState);
        Assert.Equal(ExecutionState.Stopped, h.Executor.State);
        Assert.Contains(result.Log, e => e.Message.Contains("사용자 중지"));
    }

    [Fact]
    public async Task Stop_WhileWaitingForSchedule_EndsStopped()
    {
        var h = new ExecutorHarness(new RealDelayScheduler());
        var macro = ExecutorHarness.MacroWith(new MouseClickAction());
        macro.Schedule = new Schedule { Type = ScheduleType.Once, StartTime = DateTime.Now.AddMinutes(5) };

        var run = h.Executor.RunAsync(macro);
        await Task.Delay(50);
        Assert.Equal(ExecutionState.Waiting, h.Executor.State);
        h.Executor.Stop();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ExecutionState.Stopped, result.FinalState);
        Assert.Empty(h.Input.Log);
    }

    [Fact]
    public async Task PauseResume_HoldsNextAction()
    {
        var h = new ExecutorHarness(new RealDelayScheduler());
        var macro = ExecutorHarness.MacroWith(
            new WaitAction { Milliseconds = 60 },
            new MouseClickAction { X = 1, Y = 1 },
            new MouseClickAction { X = 2, Y = 2 });

        var run = h.Executor.RunAsync(macro);
        await Task.Delay(20);
        h.Executor.Pause();
        Assert.Equal(ExecutionState.Paused, h.Executor.State);
        await Task.Delay(200);

        int startedWhilePaused;
        lock (h.StartedActions) startedWhilePaused = h.StartedActions.Count;
        Assert.Equal(1, startedWhilePaused);
        Assert.Empty(h.Input.Log);

        h.Executor.Resume();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(3, h.StartedActions.Count);
        Assert.Equal(2, h.Input.Log.Count);
        Assert.Contains(ExecutionState.Paused, h.States);
    }

    [Fact]
    public async Task StopAction_Success_CompletesAndSkipsRest()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = 1, Y = 1 },
            new StopAction { Success = true },
            new MouseClickAction { X = 2, Y = 2 });

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Single(h.Input.Log);
        Assert.Equal(1, result.ExecutedActionCount);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task StopAction_Failure_EndsError()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(new StopAction { Success = false }, new MouseClickAction());

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Error, result.FinalState);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(h.Input.Log);
    }

    [Fact]
    public async Task WindowCoordinates_ResolveAgainstClientBounds()
    {
        var h = new ExecutorHarness();
        h.Windows.Windows.Add(new WindowInfo(
            42, "Notepad", "notepad",
            new ScreenRegion { X = 100, Y = 200, Width = 800, Height = 600 },
            new ScreenRegion { X = 108, Y = 231, Width = 784, Height = 561 }));
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = 10, Y = 20, CoordinateMode = CoordinateMode.Window });
        macro.TargetWindow = new TargetWindow { TitleContains = "Notepad", BringToFront = true };

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal("Click(118,251,Left,1)", h.Input.Log.Single());
        Assert.Contains(42, h.Windows.BroughtToFront);
    }

    [Fact]
    public async Task WindowCoordinates_WithoutWindow_FallBackToScreenWithWarning()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = 10, Y = 20, CoordinateMode = CoordinateMode.Window });
        macro.TargetWindow = new TargetWindow { TitleContains = "없는창" };

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal("Click(10,20,Left,1)", h.Input.Log.Single());
        Assert.Contains(result.Log, e => e.Level == LogLevel.Warning && e.Message.Contains("대상 창"));
    }

    [Fact]
    public async Task RelativeCoordinates_AddCursorPosition()
    {
        var h = new ExecutorHarness();
        h.Input.Cursor = new ScreenPoint(500, 400);
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = -5, Y = 7, CoordinateMode = CoordinateMode.Relative });

        await h.Executor.RunAsync(macro);

        Assert.Equal("Click(495,407,Left,1)", h.Input.Log.Single());
    }

    [Fact]
    public async Task MaxRetries_RetriesWholePass_ThenErrors()
    {
        var h = new ExecutorHarness();
        h.Input.ThrowOnCall = 2;
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = 1, Y = 1 },
            new MouseClickAction { X = 2, Y = 2 });
        macro.Settings.MaxRetries = 2;
        macro.Settings.RetryIntervalMs = 123;

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(1, result.Log.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("재시도")));
        Assert.Equal(4, h.Input.Log.Count);
        Assert.Contains(123, ((ImmediateScheduler)h.Scheduler).RequestedDelays);
    }

    [Fact]
    public async Task MaxRetries_Exhausted_EndsError()
    {
        var h = new ExecutorHarness();
        var input = new AlwaysThrowingInput();
        var conditions = new ClickFlow.Engine.ConditionEngine(h.Screen, h.Windows);
        var actions = new ClickFlow.Engine.ActionExecutor(input, h.Screen, h.Scheduler, conditions, Path.GetTempPath());
        var executor = new ClickFlow.Engine.MacroExecutor(actions, h.Scheduler, h.Windows, input);
        var logs = new List<ExecutionLogEntry>();
        executor.LogEmitted += (_, e) => logs.Add(e);
        var macro = ExecutorHarness.MacroWith(new MouseClickAction());
        macro.Settings.MaxRetries = 2;

        var result = await executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Error, result.FinalState);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Equal(2, logs.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("재시도")));
        Assert.Equal(3, input.Calls);
    }

    [Fact]
    public async Task StopOnErrorFalse_ContinuesAfterFailure()
    {
        var h = new ExecutorHarness();
        h.Input.ThrowOnCall = 1;
        var macro = ExecutorHarness.MacroWith(
            new MouseClickAction { X = 1, Y = 1 },
            new MouseClickAction { X = 2, Y = 2 });
        macro.Settings.StopOnError = false;

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(1, result.ExecutedActionCount);
        Assert.Equal(2, h.Input.Log.Count);
        Assert.Contains(result.Log, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunAsync_WhileRunning_Throws()
    {
        var h = new ExecutorHarness(new RealDelayScheduler());
        var macro = ExecutorHarness.MacroWith(new WaitAction { Milliseconds = 200 });

        var run = h.Executor.RunAsync(macro);
        await Task.Delay(30);

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executor.RunAsync(macro));
        h.Executor.Stop();
        await run;
    }

    [Fact]
    public async Task IntervalSchedule_RunsRepeatCountTimes()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(new MouseClickAction { X = 1, Y = 1 });
        macro.Schedule = new Schedule { Type = ScheduleType.Interval, IntervalMs = 50, RepeatCount = 3 };

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(3, h.Input.Log.Count);
        Assert.Equal(2, ((ImmediateScheduler)h.Scheduler).RequestedDelays.Count(d => d == 50));
    }

    [Fact]
    public async Task RestoreCursor_MovesBackAfterRun()
    {
        var h = new ExecutorHarness();
        h.Input.Cursor = new ScreenPoint(5, 6);
        var macro = ExecutorHarness.MacroWith(new MouseClickAction { X = 100, Y = 100 });
        macro.Settings.RestoreCursorAfterRun = true;

        await h.Executor.RunAsync(macro);

        Assert.Equal("MoveTo(5,6)", h.Input.Log.Last());
    }

    private sealed class AlwaysThrowingInput : IInputDriver
    {
        public int Calls { get; private set; }

        public ScreenPoint GetCursorPosition() => new(0, 0);
        public void MoveTo(int x, int y) => Throw();
        public void MouseDown(MouseButton button) => Throw();
        public void MouseUp(MouseButton button) => Throw();
        public void Click(int x, int y, MouseButton button, int clickCount, bool moveFirst) => Throw();
        public void Scroll(int delta) => Throw();
        public void KeyDown(string key, KeyModifiers modifiers) => Throw();
        public void KeyUp(string key, KeyModifiers modifiers) => Throw();
        public void KeyPress(string key, KeyModifiers modifiers) => Throw();
        public void TypeText(string text, int intervalMs, CancellationToken ct) => Throw();

        private void Throw()
        {
            Calls++;
            throw new InvalidOperationException("boom");
        }
    }
}
