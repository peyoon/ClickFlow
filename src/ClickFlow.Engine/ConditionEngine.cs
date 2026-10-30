using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Engine;

public sealed class ConditionEngine : IConditionEvaluator
{
    private readonly IScreenService _screen;
    private readonly IWindowService _windows;

    public ConditionEngine(IScreenService screen, IWindowService windows)
    {
        _screen = screen;
        _windows = windows;
    }

    public bool Evaluate(Condition condition, IExecutionContext context)
    {
        bool result;
        try
        {
            result = EvaluateCore(condition, context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log(LogLevel.Warning, $"조건 평가 실패 ({condition.Describe()}): {ex.Message}");
            return false;
        }

        return condition.Negate ? !result : result;
    }

    private bool EvaluateCore(Condition condition, IExecutionContext context)
    {
        switch (condition)
        {
            case PixelColorCondition pixel:
                var actual = _screen.GetPixelColor(pixel.X, pixel.Y);
                return ColorMatches(actual, pixel.Color, pixel.Tolerance);
            case ImageExistsCondition image:
                return _screen.FindImage(image.ImagePath, image.SearchRegion, image.Similarity) is not null;
            case WindowExistsCondition window:
                return _windows.FindWindow(window.TitleContains, window.ProcessName) is not null;
            case WindowTitleCondition title:
                var foreground = _windows.GetForegroundWindow();
                return foreground is not null
                    && foreground.Title.Contains(title.TitleContains, StringComparison.OrdinalIgnoreCase);
            case TimeoutCondition timeout:
                return (DateTime.Now - context.StartedAt).TotalMilliseconds >= timeout.TimeoutMs;
            default:
                throw new NotSupportedException($"지원하지 않는 조건 유형: {condition.GetType().Name}");
        }
    }

    private static bool ColorMatches(RgbColor actual, RgbColor expected, int tolerance)
    {
        return Math.Abs(actual.R - expected.R) <= tolerance
            && Math.Abs(actual.G - expected.G) <= tolerance
            && Math.Abs(actual.B - expected.B) <= tolerance;
    }
}
