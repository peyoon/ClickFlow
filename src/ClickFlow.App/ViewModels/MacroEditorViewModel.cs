using System.IO;
using System.Collections.ObjectModel;
using ClickFlow.App.Services;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class MacroEditorViewModel : PageViewModel
{
    private readonly MacroLibrary _library;
    private readonly ExecutionService _execution;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IPickerService _picker;
    private readonly IWindowService _windows;
    private readonly IScreenService _screen;
    private bool _loading;

    public MacroEditorViewModel(
        MacroLibrary library,
        ExecutionService execution,
        INavigationService navigation,
        IDialogService dialogs,
        IPickerService picker,
        IWindowService windows,
        IScreenService screen)
    {
        _library = library;
        _execution = execution;
        _navigation = navigation;
        _dialogs = dialogs;
        _picker = picker;
        _windows = windows;
        _screen = screen;
        Macro = new Macro();
        _execution.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExecutionService.CurrentActionIndex)) HighlightCurrent();
            if (e.PropertyName == nameof(ExecutionService.State)) OnPropertyChanged(nameof(IsRunning));
        };
    }

    public override string Title => "매크로 편집";

    public IReadOnlyList<ActionKind> ActionKinds => ActionCatalog.All;
    public IReadOnlyList<ConditionKind> ConditionKinds => ConditionCatalog.All;
    public IReadOnlyList<string> KeyNames { get; } = Core.VirtualKeys.Names.OrderBy(n => n.Length).ThenBy(n => n).ToList();
    public Array ScheduleTypes { get; } = Enum.GetValues(typeof(ScheduleType));
    public Array CoordinateModes { get; } = Enum.GetValues(typeof(CoordinateMode));
    public Array MouseButtons { get; } = Enum.GetValues(typeof(MouseButton));
    public Array FailurePolicies { get; } = Enum.GetValues(typeof(FailurePolicy));

    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedPreviewPoint))]
    private ActionItemViewModel? _selected;

    [ObservableProperty]
    private Macro _macro;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<WindowInfo> _availableWindows = Array.Empty<WindowInfo>();

    public bool HasSelection => Selected is not null;
    public bool IsRunning => _execution.IsBusy;
    public ScreenPoint? SelectedPreviewPoint => Selected?.Model is PointerAction { CoordinateMode: CoordinateMode.Screen } p ? new ScreenPoint(p.X, p.Y) : null;

    public string Name
    {
        get => Macro.Name;
        set { if (Macro.Name != value) { Macro.Name = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public string? Description
    {
        get => Macro.Description;
        set { if (Macro.Description != value) { Macro.Description = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public string Profile
    {
        get => Macro.Profile;
        set { var v = string.IsNullOrWhiteSpace(value) ? "기본" : value.Trim(); if (Macro.Profile != v) { Macro.Profile = v; OnPropertyChanged(); MarkDirty(); } }
    }

    public ScheduleType ScheduleType
    {
        get => Macro.Schedule.Type;
        set
        {
            if (Macro.Schedule.Type == value) return;
            Macro.Schedule.Type = value;
            if (value == ScheduleType.Once && Macro.Schedule.StartTime is null)
            {
                var next = DateTime.Now.AddMinutes(5);
                Macro.Schedule.StartTime = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);
                OnPropertyChanged(nameof(ScheduleDate));
                OnPropertyChanged(nameof(ScheduleTime));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOnce));
            OnPropertyChanged(nameof(IsInterval));
            MarkDirty();
        }
    }

    public bool IsOnce => ScheduleType == ScheduleType.Once;
    public bool IsInterval => ScheduleType == ScheduleType.Interval;

    public DateTime? ScheduleDate
    {
        get => Macro.Schedule.StartTime?.Date;
        set
        {
            if (value is null) return;
            var time = Macro.Schedule.StartTime?.TimeOfDay ?? new TimeSpan(9, 0, 0);
            Macro.Schedule.StartTime = value.Value.Date + time;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string ScheduleTime
    {
        get => (Macro.Schedule.StartTime ?? DateTime.Today).ToString("HH:mm:ss.fff");
        set
        {
            if (!TimeSpan.TryParseExact(value.Trim(), new[] { @"hh\:mm\:ss\.fff", @"hh\:mm\:ss\.ff", @"hh\:mm\:ss\.f", @"hh\:mm\:ss", @"hh\:mm" }, null, out var time))
            {
                StatusMessage = "시간 형식: HH:mm:ss.fff (예: 08:59:59.500)";
                return;
            }
            var date = (Macro.Schedule.StartTime ?? DateTime.Today).Date;
            Macro.Schedule.StartTime = date + time;
            StatusMessage = string.Empty;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int IntervalMs
    {
        get => Macro.Schedule.IntervalMs;
        set { if (Macro.Schedule.IntervalMs != value) { Macro.Schedule.IntervalMs = Math.Max(1, value); OnPropertyChanged(); MarkDirty(); } }
    }

    public int RepeatCount
    {
        get => Macro.Schedule.RepeatCount;
        set { if (Macro.Schedule.RepeatCount != value) { Macro.Schedule.RepeatCount = Math.Max(0, value); OnPropertyChanged(); MarkDirty(); } }
    }

    public string? TargetTitle
    {
        get => Macro.TargetWindow.TitleContains;
        set { var v = string.IsNullOrWhiteSpace(value) ? null : value; if (Macro.TargetWindow.TitleContains != v) { Macro.TargetWindow.TitleContains = v; OnPropertyChanged(); MarkDirty(); } }
    }

    public string? TargetProcess
    {
        get => Macro.TargetWindow.ProcessName;
        set { var v = string.IsNullOrWhiteSpace(value) ? null : value; if (Macro.TargetWindow.ProcessName != v) { Macro.TargetWindow.ProcessName = v; OnPropertyChanged(); MarkDirty(); } }
    }

    public bool BringToFront
    {
        get => Macro.TargetWindow.BringToFront;
        set { if (Macro.TargetWindow.BringToFront != value) { Macro.TargetWindow.BringToFront = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public int MaxRetries
    {
        get => Macro.Settings.MaxRetries;
        set { if (Macro.Settings.MaxRetries != value) { Macro.Settings.MaxRetries = Math.Max(0, value); OnPropertyChanged(); MarkDirty(); } }
    }

    public int RetryIntervalMs
    {
        get => Macro.Settings.RetryIntervalMs;
        set { if (Macro.Settings.RetryIntervalMs != value) { Macro.Settings.RetryIntervalMs = Math.Max(0, value); OnPropertyChanged(); MarkDirty(); } }
    }

    public bool StopOnError
    {
        get => Macro.Settings.StopOnError;
        set { if (Macro.Settings.StopOnError != value) { Macro.Settings.StopOnError = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public bool RestoreCursorAfterRun
    {
        get => Macro.Settings.RestoreCursorAfterRun;
        set { if (Macro.Settings.RestoreCursorAfterRun != value) { Macro.Settings.RestoreCursorAfterRun = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public int DefaultDelayBetweenActionsMs
    {
        get => Macro.Settings.DefaultDelayBetweenActionsMs;
        set { if (Macro.Settings.DefaultDelayBetweenActionsMs != value) { Macro.Settings.DefaultDelayBetweenActionsMs = Math.Max(0, value); OnPropertyChanged(); MarkDirty(); } }
    }

    public void Load(Macro macro)
    {
        _loading = true;
        Macro = macro;
        Actions.Clear();
        foreach (var step in macro.Actions) Actions.Add(new ActionItemViewModel(step, MarkDirty));
        Renumber();
        Selected = Actions.FirstOrDefault();
        IsDirty = false;
        StatusMessage = string.Empty;
        _loading = false;
        RaiseAll();
    }

    public void AppendRecorded(IEnumerable<ActionStep> steps)
    {
        foreach (var step in steps) Actions.Add(new ActionItemViewModel(step, MarkDirty));
        Renumber();
        Selected = Actions.LastOrDefault();
        MarkDirty();
    }

    public override Task OnNavigatedToAsync()
    {
        RefreshWindows();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void AddAction(ActionKind? kind)
    {
        if (kind is null) return;
        var step = kind.Factory();
        var item = new ActionItemViewModel(step, MarkDirty);
        var insertAt = Selected is null ? Actions.Count : Actions.IndexOf(Selected) + 1;
        Actions.Insert(insertAt, item);
        Renumber();
        Selected = item;
        MarkDirty();
    }

    [RelayCommand]
    private void AddChild(string? slot)
    {
        if (Selected is null) return;
        var target = slot == "else" ? Selected.ElseChildren : Selected.Children;
        if (target is null) return;
        target.Add(new ActionItemViewModel(new MouseClickAction(), MarkDirty));
        Selected.SyncChildrenToModel();
        OnPropertyChanged(nameof(Selected));
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveChild(ActionItemViewModel? child)
    {
        if (child is null) return;
        foreach (var parent in Actions)
        {
            if (parent.Children?.Remove(child) == true || parent.ElseChildren?.Remove(child) == true)
            {
                parent.SyncChildrenToModel();
                MarkDirty();
                return;
            }
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if (Selected is null) return;
        var index = Actions.IndexOf(Selected);
        Actions.RemoveAt(index);
        Renumber();
        Selected = Actions.Count == 0 ? null : Actions[Math.Min(index, Actions.Count - 1)];
        MarkDirty();
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (Selected is null) return;
        var copy = new ActionItemViewModel(Selected.Model.Clone(), MarkDirty);
        Actions.Insert(Actions.IndexOf(Selected) + 1, copy);
        Renumber();
        Selected = copy;
        MarkDirty();
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    public void MoveTo(ActionItemViewModel item, int newIndex)
    {
        var oldIndex = Actions.IndexOf(item);
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Actions.Count || oldIndex == newIndex) return;
        Actions.Move(oldIndex, newIndex);
        Renumber();
        Selected = item;
        MarkDirty();
    }

    private void Move(int delta)
    {
        if (Selected is null) return;
        MoveTo(Selected, Actions.IndexOf(Selected) + delta);
    }

    [RelayCommand]
    private async Task PickPointAsync()
    {
        if (Selected is null) return;
        var point = await _picker.PickPointAsync();
        if (point is null) return;

        if (Selected.CoordinateMode == CoordinateMode.Window && Macro.TargetWindow.IsSpecified)
        {
            var window = _windows.FindWindow(Macro.TargetWindow.TitleContains, Macro.TargetWindow.ProcessName);
            if (window is not null)
            {
                Selected.SetPoint(point.X - window.ClientBounds.X, point.Y - window.ClientBounds.Y);
                return;
            }
        }
        Selected.SetPoint(point.X, point.Y);
        OnPropertyChanged(nameof(SelectedPreviewPoint));
    }

    [RelayCommand]
    private async Task PickPixelAsync()
    {
        if (Selected?.Condition is not PixelColorCondition) return;
        var point = await _picker.PickPointAsync();
        if (point is null) return;
        Selected.SetPoint(point.X, point.Y);
        try
        {
            var color = _screen.GetPixelColor(point.X, point.Y);
            Selected.PixelColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"픽셀 색상을 읽지 못했습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PickRegionAsync()
    {
        if (Selected is null) return;
        var region = await _picker.PickRegionAsync();
        if (region is null) return;
        Selected.Region = region;
    }

    [RelayCommand]
    private async Task CaptureTemplateAsync()
    {
        if (Selected?.Condition is not ImageExistsCondition) return;
        var region = await _picker.PickRegionAsync();
        if (region is null) return;
        try
        {
            var dir = Path.Combine(_library.Settings.ScreenshotDirectory, "Templates");
            var path = _screen.SaveScreenshot(dir, $"template_{DateTime.Now:yyyyMMdd_HHmmss}.png", region);
            Selected.ImagePath = path;
            StatusMessage = $"템플릿 저장: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _dialogs.Error("템플릿 캡처 실패", ex.Message);
        }
    }

    [RelayCommand]
    private void BrowseImage()
    {
        if (Selected is null) return;
        var path = _dialogs.OpenFile("이미지 (*.png;*.bmp;*.jpg)|*.png;*.bmp;*.jpg");
        if (path is not null) Selected.ImagePath = path;
    }

    [RelayCommand]
    private void ClearRegion()
    {
        if (Selected is not null) Selected.Region = null;
    }

    [RelayCommand]
    private void RefreshWindows()
    {
        try
        {
            AvailableWindows = _windows.EnumerateWindows()
                .Where(w => w.ProcessName != "ClickFlow")
                .OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            StatusMessage = $"창 목록을 가져오지 못했습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UseWindow(WindowInfo? window)
    {
        if (window is null) return;
        TargetTitle = window.Title;
        TargetProcess = window.ProcessName;
    }

    [RelayCommand]
    private void ClearTarget()
    {
        TargetTitle = null;
        TargetProcess = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        SyncToModel();
        try
        {
            await _library.SaveAsync(Macro);
            IsDirty = false;
            StatusMessage = $"저장됨 · {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _dialogs.Error("저장 실패", ex.Message);
        }
    }

    [RelayCommand]
    private async Task TestRunAsync()
    {
        if (_execution.IsBusy) return;
        SyncToModel();
        var preview = new Macro
        {
            Id = Macro.Id,
            Name = Macro.Name + " (테스트)",
            Profile = Macro.Profile,
            Schedule = new Schedule { Type = ScheduleType.Immediate },
            TargetWindow = Macro.TargetWindow,
            Settings = Macro.Settings,
            Actions = Macro.Actions,
        };
        StatusMessage = "테스트 실행 중… (F10: 중지)";
        var result = await _execution.RunAsync(preview);
        StatusMessage = result.Succeeded
            ? $"테스트 완료 · {result.ExecutedActionCount}개 동작 · {result.Duration.TotalMilliseconds:F0} ms"
            : $"테스트 {(result.FinalState == ExecutionState.Stopped ? "중지" : "실패")}: {result.ErrorMessage ?? result.FinalState.ToString()}";
        HighlightCurrent();
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (_execution.IsBusy) return;
        if (IsDirty) await SaveAsync();
        _navigation.NavigateTo(PageKind.Execution);
        await _execution.RunAsync(Macro);
    }

    [RelayCommand]
    private void Record() => _navigation.NavigateTo(PageKind.Recorder);

    [RelayCommand]
    private void Back() => _navigation.NavigateTo(PageKind.Macros);

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (!IsDirty) return true;
        if (_dialogs.Confirm("저장되지 않은 변경", "변경 내용을 저장할까요?"))
        {
            await SaveAsync();
        }
        return true;
    }

    private void SyncToModel()
    {
        foreach (var item in Actions) item.SyncChildrenToModel();
        Macro.Actions = Actions.Select(a => a.Model).ToList();
    }

    private void MarkDirty()
    {
        if (_loading) return;
        IsDirty = true;
        OnPropertyChanged(nameof(SelectedPreviewPoint));
    }

    private void Renumber()
    {
        for (var i = 0; i < Actions.Count; i++) Actions[i].Index = i;
    }

    private void HighlightCurrent()
    {
        var current = _execution.CurrentMacro?.Id == Macro.Id ? _execution.CurrentActionIndex : -1;
        foreach (var item in Actions) item.IsCurrent = item.Index == current;
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Name), nameof(Description), nameof(Profile), nameof(ScheduleType), nameof(IsOnce), nameof(IsInterval),
                     nameof(ScheduleDate), nameof(ScheduleTime), nameof(IntervalMs), nameof(RepeatCount),
                     nameof(TargetTitle), nameof(TargetProcess), nameof(BringToFront), nameof(MaxRetries), nameof(RetryIntervalMs),
                     nameof(StopOnError), nameof(RestoreCursorAfterRun), nameof(DefaultDelayBetweenActionsMs), nameof(SelectedPreviewPoint),
                 })
        {
            OnPropertyChanged(name);
        }
    }
}
