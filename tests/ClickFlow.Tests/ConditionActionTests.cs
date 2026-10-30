using ClickFlow.Core.Models;
using ClickFlow.Tests.Fakes;
using Xunit;

namespace ClickFlow.Tests;

public class ConditionActionTests
{
    private static ConditionAction PixelCondition(int retryCount = 0, FailurePolicy onFailure = FailurePolicy.Continue, bool withElse = true)
    {
        var action = new ConditionAction
        {
            Condition = new PixelColorCondition { X = 10, Y = 10, Color = new RgbColor { R = 255, G = 0, B = 0 }, Tolerance = 5 },
            ThenActions = { new KeyPressAction { Key = "T" } },
            RetryCount = retryCount,
            RetryIntervalMs = 7,
            OnFailure = onFailure,
        };
        if (withElse)
        {
            action.ElseActions.Add(new KeyPressAction { Key = "E" });
        }
        return action;
    }

    [Fact]
    public async Task MatchingPixel_RunsThenActions()
    {
        var h = new ExecutorHarness();
        h.Screen.Pixels[(10, 10)] = new RgbColor { R = 252, G = 3, B = 2 };

        var result = await h.Executor.RunAsync(ExecutorHarness.MacroWith(PixelCondition()));

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(new[] { "KeyPress(T,None)" }, h.Input.Log);
    }

    [Fact]
    public async Task NonMatchingPixel_RunsElseActions()
    {
        var h = new ExecutorHarness();

        var result = await h.Executor.RunAsync(ExecutorHarness.MacroWith(PixelCondition()));

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(new[] { "KeyPress(E,None)" }, h.Input.Log);
    }

    [Fact]
    public async Task Negate_InvertsResult()
    {
        var h = new ExecutorHarness();
        var action = PixelCondition();
        action.Condition.Negate = true;

        await h.Executor.RunAsync(ExecutorHarness.MacroWith(action));

        Assert.Equal(new[] { "KeyPress(T,None)" }, h.Input.Log);
    }

    [Fact]
    public async Task RetryCount_EvaluatesRetryPlusOneTimes()
    {
        var h = new ExecutorHarness();

        await h.Executor.RunAsync(ExecutorHarness.MacroWith(PixelCondition(retryCount: 3)));

        Assert.Equal(4, h.Screen.PixelQueries);
        Assert.Equal(3, ((ImmediateScheduler)h.Scheduler).RequestedDelays.Count(d => d == 7));
    }

    [Fact]
    public async Task OnFailureAbort_WithEmptyElse_EndsError()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            PixelCondition(onFailure: FailurePolicy.Abort, withElse: false),
            new MouseClickAction());

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Error, result.FinalState);
        Assert.Contains("조건 실패", result.ErrorMessage);
        Assert.Empty(h.Input.Log);
    }

    [Fact]
    public async Task OnFailureStop_WithEmptyElse_EndsErrorWithMessage()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            PixelCondition(onFailure: FailurePolicy.Stop, withElse: false),
            new MouseClickAction());

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Error, result.FinalState);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        Assert.Empty(h.Input.Log);
    }

    [Fact]
    public async Task OnFailureContinue_WithEmptyElse_ProceedsToNextAction()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            PixelCondition(onFailure: FailurePolicy.Continue, withElse: false),
            new MouseClickAction { X = 3, Y = 3 });

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(new[] { "Click(3,3,Left,1)" }, h.Input.Log);
    }

    [Fact]
    public async Task ImageExists_ClickOnFound_ClicksBeforeThen()
    {
        var h = new ExecutorHarness();
        h.Screen.ImageHits["btn.png"] = new ScreenPoint(640, 480);
        var action = new ConditionAction
        {
            Condition = new ImageExistsCondition { ImagePath = "btn.png", ClickOnFound = true },
            ThenActions = { new KeyPressAction { Key = "ENTER" } },
        };

        await h.Executor.RunAsync(ExecutorHarness.MacroWith(action));

        Assert.Equal(new[] { "Click(640,480,Left,1)", "KeyPress(ENTER,None)" }, h.Input.Log);
    }

    [Fact]
    public async Task WindowExists_And_WindowTitle_Evaluate()
    {
        var h = new ExecutorHarness();
        var window = new WindowInfo(1, "My Editor", "editor", new ScreenRegion(), new ScreenRegion());
        h.Windows.Windows.Add(window);
        h.Windows.Foreground = window;
        var macro = ExecutorHarness.MacroWith(
            new ConditionAction
            {
                Condition = new WindowExistsCondition { TitleContains = "editor" },
                ThenActions = { new KeyPressAction { Key = "A" } },
            },
            new ConditionAction
            {
                Condition = new WindowTitleCondition { TitleContains = "EDITOR" },
                ThenActions = { new KeyPressAction { Key = "B" } },
            },
            new ConditionAction
            {
                Condition = new WindowTitleCondition { TitleContains = "nope" },
                ThenActions = { new KeyPressAction { Key = "C" } },
            });

        await h.Executor.RunAsync(macro);

        Assert.Equal(new[] { "KeyPress(A,None)", "KeyPress(B,None)" }, h.Input.Log);
    }

    [Fact]
    public async Task RepeatAction_RunsNestedCountTimes()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(
            new RepeatAction { Count = 3, Actions = { new MouseClickAction { X = 1, Y = 1 } } });

        var result = await h.Executor.RunAsync(macro);

        Assert.Equal(ExecutionState.Completed, result.FinalState);
        Assert.Equal(3, h.Input.Log.Count);
    }

    [Fact]
    public async Task DisabledAction_IsSkipped()
    {
        var h = new ExecutorHarness();
        var macro = ExecutorHarness.MacroWith(new MouseClickAction { Enabled = false });

        var result = await h.Executor.RunAsync(macro);

        Assert.Empty(h.Input.Log);
        Assert.Contains(result.Log, e => e.Level == LogLevel.Debug);
    }
}
