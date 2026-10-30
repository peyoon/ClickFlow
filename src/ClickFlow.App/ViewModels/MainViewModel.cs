using System.Windows;
using ClickFlow.App.Services;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, INavigationService
{
    private readonly MacroLibrary _library;
    private readonly ExecutionService _execution;
    private readonly HotkeyCoordinator _hotkeys;
    private readonly Dictionary<PageKind, PageViewModel> _pages;

    public MainViewModel(
        MacroLibrary library,
        ExecutionService execution,
        HotkeyCoordinator hotkeys,
        DashboardViewModel dashboard,
        MacroListViewModel macros,
        MacroEditorViewModel editor,
        RecorderViewModel recorder,
        ExecutionViewModel executionPage,
        SettingsViewModel settings)
    {
        _library = library;
        _execution = execution;
        _hotkeys = hotkeys;
        Editor = editor;
        Recorder = recorder;
        _pages = new Dictionary<PageKind, PageViewModel>
        {
            [PageKind.Dashboard] = dashboard,
            [PageKind.Macros] = macros,
            [PageKind.Editor] = editor,
            [PageKind.Recorder] = recorder,
            [PageKind.Execution] = executionPage,
            [PageKind.Settings] = settings,
        };
        _currentPage = dashboard;
        _currentPageKind = PageKind.Dashboard;

        _execution.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExecutionService.State)) OnPropertyChanged(nameof(StatusText));
        };
        _execution.RunFinished += (_, r) => Toast(r.Succeeded
            ? $"'{r.MacroName}' 완료 · {r.ExecutedActionCount}개 동작 · {r.Duration.TotalMilliseconds:F0} ms"
            : $"'{r.MacroName}' {(r.FinalState == ExecutionState.Stopped ? "중지됨" : "실패")}{(r.ErrorMessage is null ? "" : " · " + r.ErrorMessage)}");
        _hotkeys.PanicTriggered += (_, reason) => Toast($"비상 정지: {reason}");
    }

    public MacroEditorViewModel Editor { get; }
    public RecorderViewModel Recorder { get; }
    public ExecutionService Execution => _execution;
    public AppSettings Settings => _library.Settings;

    [ObservableProperty]
    private PageViewModel _currentPage;

    [ObservableProperty]
    private PageKind _currentPageKind;

    [ObservableProperty]
    private string? _toastMessage;

    [ObservableProperty]
    private string _hotkeyHint = string.Empty;

    public string StatusText => _execution.State switch
    {
        ExecutionState.Idle => "준비",
        ExecutionState.Waiting => $"예약 대기 · {_execution.CurrentMacro?.Name}",
        ExecutionState.Running => $"실행 중 · {_execution.CurrentMacro?.Name}",
        ExecutionState.Paused => $"일시정지 · {_execution.CurrentMacro?.Name}",
        ExecutionState.Preparing => "준비 중",
        ExecutionState.Completed => "완료",
        ExecutionState.Stopped => "중지됨",
        ExecutionState.Error => "오류",
        _ => string.Empty,
    };

    public async Task InitializeAsync()
    {
        await _library.InitializeAsync();
        var failed = _hotkeys.Apply();
        _hotkeys.Attach(() => Recorder, () => Editor.Macro.Actions.Count > 0 ? Editor.Macro : _library.Macros.FirstOrDefault());
        HotkeyHint = $"{Settings.StartHotkey} 시작 · {Settings.PauseHotkey} 일시정지 · {Settings.StopHotkey} 중지 · {Settings.PanicHotkey} 비상정지";
        if (failed.Count > 0) Toast($"핫키 등록 실패: {string.Join(", ", failed)} (다른 프로그램이 사용 중)");
        if (_library.LoadError is not null) Toast("일부 매크로 파일을 읽지 못했습니다. '내 매크로'에서 확인하세요.");
        await CurrentPage.OnNavigatedToAsync();
    }

    [RelayCommand]
    public void NavigateTo(PageKind page)
    {
        if (!_pages.TryGetValue(page, out var target)) return;
        _ = NavigateCoreAsync(page, target);
    }

    public void OpenMacro(Macro macro)
    {
        Editor.Load(macro);
        NavigateTo(PageKind.Editor);
    }

    [RelayCommand]
    private void DismissToast() => ToastMessage = null;

    public void Toast(string message)
    {
        ToastMessage = message;
        var captured = message;
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(4000);
            if (ToastMessage == captured) ToastMessage = null;
        });
    }

    private async Task NavigateCoreAsync(PageKind page, PageViewModel target)
    {
        if (ReferenceEquals(CurrentPage, target)) return;
        if (CurrentPage is MacroEditorViewModel editor && !await editor.ConfirmLeaveAsync()) return;
        CurrentPage.OnNavigatedFrom();
        CurrentPage = target;
        CurrentPageKind = page;
        await target.OnNavigatedToAsync();
    }
}
