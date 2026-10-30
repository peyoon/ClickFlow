using ClickFlow.Core;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Engine;

public sealed class ActionExecutor
{
    private const int MoveStepMs = 8;

    private readonly IInputDriver _input;
    private readonly IScreenService _screen;
    private readonly IScheduler _scheduler;
    private readonly IConditionEvaluator _conditions;
    private readonly Func<string> _screenshotDirectory;

    public ActionExecutor(
        IInputDriver input,
        IScreenService screen,
        IScheduler scheduler,
        IConditionEvaluator conditions,
        Func<string> screenshotDirectory)
    {
        _input = input;
        _screen = screen;
        _scheduler = scheduler;
        _conditions = conditions;
        _screenshotDirectory = screenshotDirectory;
    }

    public ActionExecutor(
        IInputDriver input,
        IScreenService screen,
        IScheduler scheduler,
        IConditionEvaluator conditions,
        string screenshotDirectory)
        : this(input, screen, scheduler, conditions, () => screenshotDirectory)
    {
    }

    public async Task ExecuteAsync(ActionStep step, IExecutionContext ctx, int index)
    {
        if (!step.Enabled)
        {
            ctx.Log(LogLevel.Debug, $"비활성화된 동작 건너뜀: {step.DisplayName}", index);
            return;
        }

        await ctx.WaitIfPausedAsync().ConfigureAwait(false);
        ctx.ThrowIfStopRequested();

        var ct = ctx.CancellationToken;
        if (step.DelayBeforeMs > 0)
        {
            await _scheduler.DelayAsync(step.DelayBeforeMs, ct).ConfigureAwait(false);
        }

        await ExecuteCoreAsync(step, ctx, index).ConfigureAwait(false);

        var delayAfter = step.DelayAfterMs;
        if (step.RandomDelayMaxMs > 0)
        {
            delayAfter += Random.Shared.Next(0, step.RandomDelayMaxMs + 1);
        }
        if (delayAfter > 0)
        {
            await _scheduler.DelayAsync(delayAfter, ct).ConfigureAwait(false);
        }

        await ctx.WaitIfPausedAsync().ConfigureAwait(false);
        ctx.ThrowIfStopRequested();
    }

    private async Task ExecuteCoreAsync(ActionStep step, IExecutionContext ctx, int index)
    {
        var ct = ctx.CancellationToken;
        switch (step)
        {
            case MouseClickAction click:
            {
                var p = ctx.ResolvePoint(click.X, click.Y, click.CoordinateMode);
                LogAction(ctx, index, step, $"{p.X}, {p.Y} · {click.Button}");
                _input.Click(p.X, p.Y, click.Button, click.ClickCount, click.MoveBeforeClick);
                break;
            }
            case MouseMoveAction move:
            {
                var p = ctx.ResolvePoint(move.X, move.Y, move.CoordinateMode);
                LogAction(ctx, index, step, $"{p.X}, {p.Y}");
                await MoveAsync(p, move.DurationMs, ct).ConfigureAwait(false);
                break;
            }
            case MouseDownAction down:
            {
                var p = ctx.ResolvePoint(down.X, down.Y, down.CoordinateMode);
                LogAction(ctx, index, step, $"{p.X}, {p.Y} · {down.Button}");
                _input.MoveTo(p.X, p.Y);
                _input.MouseDown(down.Button);
                break;
            }
            case MouseUpAction up:
            {
                var p = ctx.ResolvePoint(up.X, up.Y, up.CoordinateMode);
                LogAction(ctx, index, step, $"{p.X}, {p.Y} · {up.Button}");
                _input.MoveTo(p.X, p.Y);
                _input.MouseUp(up.Button);
                break;
            }
            case MouseScrollAction scroll:
                LogAction(ctx, index, step, step.DisplayValue);
                _input.Scroll(scroll.Delta);
                break;
            case KeyPressAction keyPress:
                LogAction(ctx, index, step, step.DisplayValue);
                _input.KeyPress(keyPress.Key, ToModifiers(keyPress));
                break;
            case KeyDownAction keyDown:
                LogAction(ctx, index, step, step.DisplayValue);
                _input.KeyDown(keyDown.Key, ToModifiers(keyDown));
                break;
            case KeyUpAction keyUp:
                LogAction(ctx, index, step, step.DisplayValue);
                _input.KeyUp(keyUp.Key, ToModifiers(keyUp));
                break;
            case TypeTextAction text:
                LogAction(ctx, index, step, step.DisplayValue);
                _input.TypeText(text.Text, text.IntervalMs, ct);
                break;
            case WaitAction wait:
                LogAction(ctx, index, step, step.DisplayValue);
                await _scheduler.DelayAsync(wait.Milliseconds, ct).ConfigureAwait(false);
                break;
            case ScreenshotAction screenshot:
            {
                var path = _screen.SaveScreenshot(_screenshotDirectory(), screenshot.FileName, screenshot.Region);
                LogAction(ctx, index, step, $"저장됨: {path}");
                break;
            }
            case ConditionAction condition:
                await ExecuteConditionAsync(condition, ctx, index).ConfigureAwait(false);
                break;
            case RepeatAction repeat:
                await ExecuteRepeatAsync(repeat, ctx, index).ConfigureAwait(false);
                break;
            case StopAction stop:
                LogAction(ctx, index, step, step.DisplayValue);
                throw new MacroCompletedException(stop.Success);
            default:
                throw new NotSupportedException($"지원하지 않는 동작 유형: {step.GetType().Name}");
        }
    }

    private async Task MoveAsync(ScreenPoint target, int durationMs, CancellationToken ct)
    {
        if (durationMs <= 0)
        {
            _input.MoveTo(target.X, target.Y);
            return;
        }

        var start = _input.GetCursorPosition();
        var steps = Math.Max(1, durationMs / MoveStepMs);
        for (var i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = (double)i / steps;
            var x = (int)Math.Round(start.X + (target.X - start.X) * t);
            var y = (int)Math.Round(start.Y + (target.Y - start.Y) * t);
            _input.MoveTo(x, y);
            if (i < steps)
            {
                await _scheduler.DelayAsync(MoveStepMs, ct).ConfigureAwait(false);
            }
        }
        _input.MoveTo(target.X, target.Y);
    }

    private async Task ExecuteConditionAsync(ConditionAction action, IExecutionContext ctx, int index)
    {
        var ct = ctx.CancellationToken;
        var condition = action.Condition;
        var attempts = Math.Max(0, action.RetryCount) + 1;
        var matched = false;
        ScreenPoint? foundPoint = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ctx.ThrowIfStopRequested();
            matched = Evaluate(condition, ctx, out foundPoint);
            if (matched)
            {
                break;
            }

            if (attempt < attempts)
            {
                ctx.Log(LogLevel.Debug, $"조건 불일치, 재시도 {attempt}/{action.RetryCount}: {condition.Describe()}", index);
                await _scheduler.DelayAsync(action.RetryIntervalMs, ct).ConfigureAwait(false);
            }
        }

        ctx.Log(LogLevel.Info, $"조건 {(matched ? "일치" : "불일치")}: {condition.Describe()}", index);

        if (matched)
        {
            if (foundPoint is not null)
            {
                ctx.Log(LogLevel.Info, $"발견 위치 클릭: {foundPoint.X}, {foundPoint.Y}", index);
                _input.Click(foundPoint.X, foundPoint.Y, MouseButton.Left, 1, true);
            }
            await ExecuteNestedAsync(action.ThenActions, ctx, index).ConfigureAwait(false);
            return;
        }

        if (action.ElseActions.Count > 0)
        {
            await ExecuteNestedAsync(action.ElseActions, ctx, index).ConfigureAwait(false);
            return;
        }

        switch (action.OnFailure)
        {
            case FailurePolicy.Abort:
                throw new MacroAbortedException($"조건 실패: {condition.Describe()}");
            case FailurePolicy.Stop:
                throw new MacroCompletedException(false);
        }
    }

    private bool Evaluate(Condition condition, IExecutionContext ctx, out ScreenPoint? foundPoint)
    {
        foundPoint = null;
        if (condition is ImageExistsCondition { ClickOnFound: true, Negate: false } image)
        {
            try
            {
                foundPoint = _screen.FindImage(image.ImagePath, image.SearchRegion, image.Similarity);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Log(LogLevel.Warning, $"이미지 검색 실패 ({condition.Describe()}): {ex.Message}");
                return false;
            }
            return foundPoint is not null;
        }

        return _conditions.Evaluate(condition, ctx);
    }

    private async Task ExecuteRepeatAsync(RepeatAction repeat, IExecutionContext ctx, int index)
    {
        var infinite = repeat.Count <= 0;
        for (var iteration = 1; infinite || iteration <= repeat.Count; iteration++)
        {
            ctx.ThrowIfStopRequested();
            ctx.Log(LogLevel.Info, infinite ? $"반복 {iteration}회차" : $"반복 {iteration}/{repeat.Count}회차", index);
            await ExecuteNestedAsync(repeat.Actions, ctx, index).ConfigureAwait(false);
        }
    }

    private async Task ExecuteNestedAsync(List<ActionStep> steps, IExecutionContext ctx, int parentIndex)
    {
        foreach (var step in steps)
        {
            await ExecuteAsync(step, ctx, parentIndex).ConfigureAwait(false);
        }
    }

    private static void LogAction(IExecutionContext ctx, int index, ActionStep step, string detail)
    {
        ctx.Log(LogLevel.Info, $"동작 #{index + 1} {step.DisplayName} {detail}", index);
    }

    private static KeyModifiers ToModifiers(KeyboardAction action)
    {
        var modifiers = KeyModifiers.None;
        if (action.Ctrl) modifiers |= KeyModifiers.Ctrl;
        if (action.Shift) modifiers |= KeyModifiers.Shift;
        if (action.Alt) modifiers |= KeyModifiers.Alt;
        if (action.Win) modifiers |= KeyModifiers.Win;
        return modifiers;
    }
}
