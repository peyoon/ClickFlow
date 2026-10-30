using System.Text.Json.Serialization;

namespace ClickFlow.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(MouseClickAction), "Click")]
[JsonDerivedType(typeof(MouseMoveAction), "Move")]
[JsonDerivedType(typeof(MouseDownAction), "MouseDown")]
[JsonDerivedType(typeof(MouseUpAction), "MouseUp")]
[JsonDerivedType(typeof(MouseScrollAction), "Scroll")]
[JsonDerivedType(typeof(KeyPressAction), "Key")]
[JsonDerivedType(typeof(KeyDownAction), "KeyDown")]
[JsonDerivedType(typeof(KeyUpAction), "KeyUp")]
[JsonDerivedType(typeof(TypeTextAction), "Text")]
[JsonDerivedType(typeof(WaitAction), "Wait")]
[JsonDerivedType(typeof(ScreenshotAction), "Screenshot")]
[JsonDerivedType(typeof(ConditionAction), "Condition")]
[JsonDerivedType(typeof(RepeatAction), "Repeat")]
[JsonDerivedType(typeof(StopAction), "Stop")]
public abstract class ActionStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string? Comment { get; set; }
    public int DelayBeforeMs { get; set; }
    public int DelayAfterMs { get; set; }
    public int RandomDelayMaxMs { get; set; }

    [JsonIgnore]
    public abstract string DisplayName { get; }

    [JsonIgnore]
    public abstract string DisplayValue { get; }

    public virtual ActionStep Clone()
    {
        var copy = (ActionStep)MemberwiseClone();
        copy.Id = Guid.NewGuid();
        return copy;
    }
}

public abstract class PointerAction : ActionStep
{
    public int X { get; set; }
    public int Y { get; set; }
    public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Screen;

    public override string DisplayValue => CoordinateMode switch
    {
        CoordinateMode.Window => $"창 기준 {X}, {Y}",
        CoordinateMode.Relative => $"상대 {X:+#;-#;0}, {Y:+#;-#;0}",
        _ => $"{X}, {Y}",
    };
}

public sealed class MouseClickAction : PointerAction
{
    public MouseButton Button { get; set; } = MouseButton.Left;
    public int ClickCount { get; set; } = 1;
    public bool MoveBeforeClick { get; set; } = true;

    public override string DisplayName => ClickCount >= 2 ? "더블 클릭" : "마우스 클릭";
    public override string DisplayValue => $"{base.DisplayValue} · {Button}";
}

public sealed class MouseMoveAction : PointerAction
{
    public int DurationMs { get; set; }

    public override string DisplayName => "마우스 이동";
}

public sealed class MouseDownAction : PointerAction
{
    public MouseButton Button { get; set; } = MouseButton.Left;

    public override string DisplayName => "마우스 누름";
    public override string DisplayValue => $"{base.DisplayValue} · {Button}";
}

public sealed class MouseUpAction : PointerAction
{
    public MouseButton Button { get; set; } = MouseButton.Left;

    public override string DisplayName => "마우스 뗌";
    public override string DisplayValue => $"{base.DisplayValue} · {Button}";
}

public sealed class MouseScrollAction : ActionStep
{
    public int Delta { get; set; } = -120;

    public override string DisplayName => "스크롤";
    public override string DisplayValue => Delta > 0 ? $"위로 {Delta / 120}" : $"아래로 {-Delta / 120}";
}

public abstract class KeyboardAction : ActionStep
{
    public string Key { get; set; } = "ENTER";
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Win { get; set; }

    public override string DisplayValue
    {
        get
        {
            var parts = new List<string>(5);
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            if (Win) parts.Add("Win");
            parts.Add(Key);
            return string.Join(" + ", parts);
        }
    }
}

public sealed class KeyPressAction : KeyboardAction
{
    public override string DisplayName => "키 입력";
}

public sealed class KeyDownAction : KeyboardAction
{
    public override string DisplayName => "키 누름";
}

public sealed class KeyUpAction : KeyboardAction
{
    public override string DisplayName => "키 뗌";
}

public sealed class TypeTextAction : ActionStep
{
    public string Text { get; set; } = string.Empty;
    public int IntervalMs { get; set; } = 10;

    public override string DisplayName => "문자열 입력";
    public override string DisplayValue => Text.Length > 24 ? Text[..24] + "…" : Text;
}

public sealed class WaitAction : ActionStep
{
    public int Milliseconds { get; set; } = 100;

    public override string DisplayName => "대기";
    public override string DisplayValue => $"{Milliseconds} ms";
}

public sealed class ScreenshotAction : ActionStep
{
    public string? FileName { get; set; }
    public ScreenRegion? Region { get; set; }

    public override string DisplayName => "스크린샷";
    public override string DisplayValue => FileName ?? "자동 파일명";
}

public sealed class ConditionAction : ActionStep
{
    public Condition Condition { get; set; } = new PixelColorCondition();
    public List<ActionStep> ThenActions { get; set; } = new();
    public List<ActionStep> ElseActions { get; set; } = new();
    public int RetryCount { get; set; }
    public int RetryIntervalMs { get; set; } = 200;
    public FailurePolicy OnFailure { get; set; } = FailurePolicy.Continue;

    public override string DisplayName => "조건";
    public override string DisplayValue => Condition.Describe();

    public override ActionStep Clone()
    {
        var copy = (ConditionAction)base.Clone();
        copy.Condition = Condition.Clone();
        copy.ThenActions = ThenActions.Select(a => a.Clone()).ToList();
        copy.ElseActions = ElseActions.Select(a => a.Clone()).ToList();
        return copy;
    }
}

public sealed class RepeatAction : ActionStep
{
    public int Count { get; set; } = 1;
    public List<ActionStep> Actions { get; set; } = new();

    public override string DisplayName => "반복";
    public override string DisplayValue => Count <= 0 ? "무한 반복" : $"{Count}회 · {Actions.Count}개 동작";

    public override ActionStep Clone()
    {
        var copy = (RepeatAction)base.Clone();
        copy.Actions = Actions.Select(a => a.Clone()).ToList();
        return copy;
    }
}

public sealed class StopAction : ActionStep
{
    public bool Success { get; set; } = true;

    public override string DisplayName => "종료";
    public override string DisplayValue => Success ? "성공으로 종료" : "실패로 종료";
}
