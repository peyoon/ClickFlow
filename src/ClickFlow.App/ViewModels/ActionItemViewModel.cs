using System.Collections.ObjectModel;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClickFlow.App.ViewModels;

public sealed record ActionKind(string Id, string Label, Func<ActionStep> Factory);

public static class ActionCatalog
{
    public static readonly IReadOnlyList<ActionKind> All = new ActionKind[]
    {
        new("Click", "마우스 클릭", () => new MouseClickAction()),
        new("DoubleClick", "더블 클릭", () => new MouseClickAction { ClickCount = 2 }),
        new("Move", "마우스 이동", () => new MouseMoveAction()),
        new("MouseDown", "마우스 누름", () => new MouseDownAction()),
        new("MouseUp", "마우스 뗌", () => new MouseUpAction()),
        new("Scroll", "스크롤", () => new MouseScrollAction()),
        new("Key", "키 입력", () => new KeyPressAction()),
        new("KeyDown", "키 누름", () => new KeyDownAction()),
        new("KeyUp", "키 뗌", () => new KeyUpAction()),
        new("Text", "문자열 입력", () => new TypeTextAction()),
        new("Wait", "대기", () => new WaitAction()),
        new("Screenshot", "스크린샷", () => new ScreenshotAction()),
        new("Condition", "조건 (IF)", () => new ConditionAction()),
        new("Repeat", "반복", () => new RepeatAction { Count = 3 }),
        new("Stop", "종료", () => new StopAction()),
    };

    public static string KindOf(ActionStep step) => step switch
    {
        MouseClickAction => "Click",
        MouseMoveAction => "Move",
        MouseDownAction => "MouseDown",
        MouseUpAction => "MouseUp",
        MouseScrollAction => "Scroll",
        KeyPressAction => "Key",
        KeyDownAction => "KeyDown",
        KeyUpAction => "KeyUp",
        TypeTextAction => "Text",
        WaitAction => "Wait",
        ScreenshotAction => "Screenshot",
        ConditionAction => "Condition",
        RepeatAction => "Repeat",
        StopAction => "Stop",
        _ => "Unknown",
    };
}

public sealed record ConditionKind(string Id, string Label, Func<Condition> Factory);

public static class ConditionCatalog
{
    public static readonly IReadOnlyList<ConditionKind> All = new ConditionKind[]
    {
        new("PixelColor", "픽셀 색상", () => new PixelColorCondition()),
        new("ImageExists", "이미지 존재", () => new ImageExistsCondition()),
        new("WindowExists", "창 존재", () => new WindowExistsCondition()),
        new("WindowTitle", "활성 창 제목", () => new WindowTitleCondition()),
        new("Timeout", "경과 시간", () => new TimeoutCondition()),
    };

    public static string KindOf(Condition condition) => condition switch
    {
        PixelColorCondition => "PixelColor",
        ImageExistsCondition => "ImageExists",
        WindowExistsCondition => "WindowExists",
        WindowTitleCondition => "WindowTitle",
        TimeoutCondition => "Timeout",
        _ => "Unknown",
    };
}

public sealed partial class ActionItemViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public ActionItemViewModel(ActionStep model, Action markDirty)
    {
        Model = model;
        _markDirty = markDirty;
        Kind = ActionCatalog.KindOf(model);

        if (model is RepeatAction repeat)
        {
            Children = new ObservableCollection<ActionItemViewModel>(repeat.Actions.Select(a => new ActionItemViewModel(a, markDirty)));
        }
        else if (model is ConditionAction condition)
        {
            Children = new ObservableCollection<ActionItemViewModel>(condition.ThenActions.Select(a => new ActionItemViewModel(a, markDirty)));
            ElseChildren = new ObservableCollection<ActionItemViewModel>(condition.ElseActions.Select(a => new ActionItemViewModel(a, markDirty)));
            ConditionKind = ConditionCatalog.KindOf(condition.Condition);
        }
    }

    public ActionStep Model { get; private set; }
    public string Kind { get; }
    public ObservableCollection<ActionItemViewModel>? Children { get; }
    public ObservableCollection<ActionItemViewModel>? ElseChildren { get; }

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private bool _isCurrent;

    public string DisplayName => Model.DisplayName;
    public string DisplayValue => Model.DisplayValue;

    public bool IsPointer => Model is PointerAction;
    public bool IsClick => Model is MouseClickAction;
    public bool IsMove => Model is MouseMoveAction;
    public bool HasButton => Model is MouseClickAction or MouseDownAction or MouseUpAction;
    public bool IsScroll => Model is MouseScrollAction;
    public bool IsKeyboard => Model is KeyboardAction;
    public bool IsText => Model is TypeTextAction;
    public bool IsWait => Model is WaitAction;
    public bool IsScreenshot => Model is ScreenshotAction;
    public bool IsCondition => Model is ConditionAction;
    public bool IsRepeat => Model is RepeatAction;
    public bool IsStop => Model is StopAction;
    public bool HasChildren => Children is not null;

    public bool Enabled
    {
        get => Model.Enabled;
        set => Set(Model.Enabled, value, v => Model.Enabled = v);
    }

    public string? Comment
    {
        get => Model.Comment;
        set => Set(Model.Comment, value, v => Model.Comment = string.IsNullOrWhiteSpace(v) ? null : v);
    }

    public int DelayBeforeMs
    {
        get => Model.DelayBeforeMs;
        set => Set(Model.DelayBeforeMs, Math.Max(0, value), v => Model.DelayBeforeMs = v);
    }

    public int DelayAfterMs
    {
        get => Model.DelayAfterMs;
        set => Set(Model.DelayAfterMs, Math.Max(0, value), v => Model.DelayAfterMs = v);
    }

    public int RandomDelayMaxMs
    {
        get => Model.RandomDelayMaxMs;
        set => Set(Model.RandomDelayMaxMs, Math.Max(0, value), v => Model.RandomDelayMaxMs = v);
    }

    public int X
    {
        get => (Model as PointerAction)?.X ?? 0;
        set { if (Model is PointerAction p) Set(p.X, value, v => p.X = v); }
    }

    public int Y
    {
        get => (Model as PointerAction)?.Y ?? 0;
        set { if (Model is PointerAction p) Set(p.Y, value, v => p.Y = v); }
    }

    public CoordinateMode CoordinateMode
    {
        get => (Model as PointerAction)?.CoordinateMode ?? CoordinateMode.Screen;
        set { if (Model is PointerAction p) Set(p.CoordinateMode, value, v => p.CoordinateMode = v); }
    }

    public MouseButton Button
    {
        get => Model switch
        {
            MouseClickAction c => c.Button,
            MouseDownAction d => d.Button,
            MouseUpAction u => u.Button,
            _ => MouseButton.Left,
        };
        set
        {
            switch (Model)
            {
                case MouseClickAction c: Set(c.Button, value, v => c.Button = v); break;
                case MouseDownAction d: Set(d.Button, value, v => d.Button = v); break;
                case MouseUpAction u: Set(u.Button, value, v => u.Button = v); break;
            }
        }
    }

    public int ClickCount
    {
        get => (Model as MouseClickAction)?.ClickCount ?? 1;
        set { if (Model is MouseClickAction c) Set(c.ClickCount, Math.Clamp(value, 1, 3), v => c.ClickCount = v); }
    }

    public bool MoveBeforeClick
    {
        get => (Model as MouseClickAction)?.MoveBeforeClick ?? true;
        set { if (Model is MouseClickAction c) Set(c.MoveBeforeClick, value, v => c.MoveBeforeClick = v); }
    }

    public int DurationMs
    {
        get => (Model as MouseMoveAction)?.DurationMs ?? 0;
        set { if (Model is MouseMoveAction m) Set(m.DurationMs, Math.Max(0, value), v => m.DurationMs = v); }
    }

    public int ScrollNotches
    {
        get => (Model as MouseScrollAction)?.Delta / 120 ?? 0;
        set { if (Model is MouseScrollAction s) Set(s.Delta, value * 120, v => s.Delta = v); }
    }

    public string Key
    {
        get => (Model as KeyboardAction)?.Key ?? string.Empty;
        set { if (Model is KeyboardAction k) Set(k.Key, value.Trim().ToUpperInvariant(), v => k.Key = v); }
    }

    public bool Ctrl
    {
        get => (Model as KeyboardAction)?.Ctrl ?? false;
        set { if (Model is KeyboardAction k) Set(k.Ctrl, value, v => k.Ctrl = v); }
    }

    public bool Shift
    {
        get => (Model as KeyboardAction)?.Shift ?? false;
        set { if (Model is KeyboardAction k) Set(k.Shift, value, v => k.Shift = v); }
    }

    public bool Alt
    {
        get => (Model as KeyboardAction)?.Alt ?? false;
        set { if (Model is KeyboardAction k) Set(k.Alt, value, v => k.Alt = v); }
    }

    public bool Win
    {
        get => (Model as KeyboardAction)?.Win ?? false;
        set { if (Model is KeyboardAction k) Set(k.Win, value, v => k.Win = v); }
    }

    public string Text
    {
        get => (Model as TypeTextAction)?.Text ?? string.Empty;
        set { if (Model is TypeTextAction t) Set(t.Text, value, v => t.Text = v); }
    }

    public int IntervalMs
    {
        get => (Model as TypeTextAction)?.IntervalMs ?? 0;
        set { if (Model is TypeTextAction t) Set(t.IntervalMs, Math.Max(0, value), v => t.IntervalMs = v); }
    }

    public int Milliseconds
    {
        get => (Model as WaitAction)?.Milliseconds ?? 0;
        set { if (Model is WaitAction w) Set(w.Milliseconds, Math.Max(0, value), v => w.Milliseconds = v); }
    }

    public string? FileName
    {
        get => (Model as ScreenshotAction)?.FileName;
        set { if (Model is ScreenshotAction s) Set(s.FileName, value, v => s.FileName = string.IsNullOrWhiteSpace(v) ? null : v); }
    }

    public int RepeatCount
    {
        get => (Model as RepeatAction)?.Count ?? 1;
        set { if (Model is RepeatAction r) Set(r.Count, Math.Max(0, value), v => r.Count = v); }
    }

    public bool StopSuccess
    {
        get => (Model as StopAction)?.Success ?? true;
        set { if (Model is StopAction s) Set(s.Success, value, v => s.Success = v); }
    }

    public int RetryCount
    {
        get => (Model as ConditionAction)?.RetryCount ?? 0;
        set { if (Model is ConditionAction c) Set(c.RetryCount, Math.Max(0, value), v => c.RetryCount = v); }
    }

    public int RetryIntervalMs
    {
        get => (Model as ConditionAction)?.RetryIntervalMs ?? 0;
        set { if (Model is ConditionAction c) Set(c.RetryIntervalMs, Math.Max(0, value), v => c.RetryIntervalMs = v); }
    }

    public FailurePolicy OnFailure
    {
        get => (Model as ConditionAction)?.OnFailure ?? FailurePolicy.Continue;
        set { if (Model is ConditionAction c) Set(c.OnFailure, value, v => c.OnFailure = v); }
    }

    private string _conditionKind = "PixelColor";
    public string ConditionKind
    {
        get => _conditionKind;
        set
        {
            if (Model is not ConditionAction c || _conditionKind == value) return;
            var kind = ConditionCatalog.All.FirstOrDefault(k => k.Id == value);
            if (kind is null) return;
            _conditionKind = value;
            c.Condition = kind.Factory();
            OnPropertyChanged();
            RaiseConditionProperties();
            _markDirty();
        }
    }

    public Condition? Condition => (Model as ConditionAction)?.Condition;
    public bool IsPixelCondition => Condition is PixelColorCondition;
    public bool IsImageCondition => Condition is ImageExistsCondition;
    public bool IsWindowExistsCondition => Condition is WindowExistsCondition;
    public bool IsWindowTitleCondition => Condition is WindowTitleCondition;
    public bool IsTimeoutCondition => Condition is TimeoutCondition;

    public bool Negate
    {
        get => Condition?.Negate ?? false;
        set { if (Condition is { } c) Set(c.Negate, value, v => c.Negate = v); }
    }

    public int PixelX
    {
        get => (Condition as PixelColorCondition)?.X ?? 0;
        set { if (Condition is PixelColorCondition p) Set(p.X, value, v => p.X = v); }
    }

    public int PixelY
    {
        get => (Condition as PixelColorCondition)?.Y ?? 0;
        set { if (Condition is PixelColorCondition p) Set(p.Y, value, v => p.Y = v); }
    }

    public string PixelColorHex
    {
        get => Condition is PixelColorCondition p ? $"#{p.Color.R:X2}{p.Color.G:X2}{p.Color.B:X2}" : "#000000";
        set
        {
            if (Condition is not PixelColorCondition p) return;
            var hex = value.TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return;
            p.Color = new RgbColor { R = (byte)(rgb >> 16), G = (byte)(rgb >> 8), B = (byte)rgb };
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayValue));
            _markDirty();
        }
    }

    public int Tolerance
    {
        get => (Condition as PixelColorCondition)?.Tolerance ?? 0;
        set { if (Condition is PixelColorCondition p) Set(p.Tolerance, Math.Clamp(value, 0, 255), v => p.Tolerance = v); }
    }

    public string ImagePath
    {
        get => (Condition as ImageExistsCondition)?.ImagePath ?? string.Empty;
        set { if (Condition is ImageExistsCondition i) Set(i.ImagePath, value, v => i.ImagePath = v); }
    }

    public double Similarity
    {
        get => (Condition as ImageExistsCondition)?.Similarity ?? 0.9;
        set { if (Condition is ImageExistsCondition i) Set(i.Similarity, Math.Clamp(value, 0.1, 1.0), v => i.Similarity = v); }
    }

    public bool ClickOnFound
    {
        get => (Condition as ImageExistsCondition)?.ClickOnFound ?? false;
        set { if (Condition is ImageExistsCondition i) Set(i.ClickOnFound, value, v => i.ClickOnFound = v); }
    }

    public string? ConditionTitle
    {
        get => Condition switch
        {
            WindowExistsCondition w => w.TitleContains,
            WindowTitleCondition t => t.TitleContains,
            _ => null,
        };
        set
        {
            switch (Condition)
            {
                case WindowExistsCondition w: Set(w.TitleContains, value, v => w.TitleContains = string.IsNullOrWhiteSpace(v) ? null : v); break;
                case WindowTitleCondition t: Set(t.TitleContains, value ?? string.Empty, v => t.TitleContains = v); break;
            }
        }
    }

    public string? ConditionProcess
    {
        get => (Condition as WindowExistsCondition)?.ProcessName;
        set { if (Condition is WindowExistsCondition w) Set(w.ProcessName, value, v => w.ProcessName = string.IsNullOrWhiteSpace(v) ? null : v); }
    }

    public int TimeoutMs
    {
        get => (Condition as TimeoutCondition)?.TimeoutMs ?? 0;
        set { if (Condition is TimeoutCondition t) Set(t.TimeoutMs, Math.Max(0, value), v => t.TimeoutMs = v); }
    }

    public ScreenRegion? Region
    {
        get => Model switch
        {
            ScreenshotAction s => s.Region,
            ConditionAction { Condition: ImageExistsCondition i } => i.SearchRegion,
            _ => null,
        };
        set
        {
            switch (Model)
            {
                case ScreenshotAction s: s.Region = value; break;
                case ConditionAction { Condition: ImageExistsCondition i }: i.SearchRegion = value; break;
                default: return;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(RegionText));
            _markDirty();
        }
    }

    public string RegionText => Region?.ToString() ?? "전체 화면";

    public void SetPoint(int x, int y)
    {
        if (Model is PointerAction p)
        {
            p.X = x;
            p.Y = y;
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
        }
        else if (Condition is PixelColorCondition pc)
        {
            pc.X = x;
            pc.Y = y;
            OnPropertyChanged(nameof(PixelX));
            OnPropertyChanged(nameof(PixelY));
        }
        OnPropertyChanged(nameof(DisplayValue));
        _markDirty();
    }

    public void SyncChildrenToModel()
    {
        switch (Model)
        {
            case RepeatAction repeat when Children is not null:
                repeat.Actions = Children.Select(c => { c.SyncChildrenToModel(); return c.Model; }).ToList();
                break;
            case ConditionAction condition when Children is not null && ElseChildren is not null:
                condition.ThenActions = Children.Select(c => { c.SyncChildrenToModel(); return c.Model; }).ToList();
                condition.ElseActions = ElseChildren.Select(c => { c.SyncChildrenToModel(); return c.Model; }).ToList();
                break;
        }
    }

    private void Set<T>(T current, T value, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        apply(value);
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(DisplayValue));
        _markDirty();
    }

    private void RaiseConditionProperties()
    {
        foreach (var name in new[]
                 {
                     nameof(Condition), nameof(IsPixelCondition), nameof(IsImageCondition), nameof(IsWindowExistsCondition),
                     nameof(IsWindowTitleCondition), nameof(IsTimeoutCondition), nameof(Negate), nameof(PixelX), nameof(PixelY),
                     nameof(PixelColorHex), nameof(Tolerance), nameof(ImagePath), nameof(Similarity), nameof(ClickOnFound),
                     nameof(ConditionTitle), nameof(ConditionProcess), nameof(TimeoutMs), nameof(Region), nameof(RegionText),
                     nameof(DisplayValue),
                 })
        {
            OnPropertyChanged(name);
        }
    }
}
