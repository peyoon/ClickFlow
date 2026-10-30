using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ClickFlow.App.Services;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class RecorderViewModel : PageViewModel
{
    private readonly IInputRecorder _recorder;
    private readonly MacroLibrary _library;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly MacroEditorViewModel _editor;
    private readonly DispatcherTimer _clock;
    private DateTime _startedAt;

    public RecorderViewModel(IInputRecorder recorder, MacroLibrary library, INavigationService navigation, IDialogService dialogs, MacroEditorViewModel editor)
    {
        _recorder = recorder;
        _library = library;
        _navigation = navigation;
        _dialogs = dialogs;
        _editor = editor;
        _recorder.EventCaptured += OnEventCaptured;
        _clock = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        _clock.Tick += (_, _) => Elapsed = DateTime.Now - _startedAt;
    }

    public override string Title => "레코더";

    public ObservableCollection<RecordedEvent> Events { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUse))]
    private bool _isRecording;

    [ObservableProperty]
    private TimeSpan _elapsed;

    [ObservableProperty]
    private bool _recordMouseMoves;

    [ObservableProperty]
    private string _statusMessage = "녹화를 시작하면 ClickFlow 창은 최소화되고, 중지 핫키(기본 F10)로 종료합니다.";

    [ObservableProperty]
    private string _newMacroName = string.Empty;

    private IReadOnlyList<ActionStep> _captured = Array.Empty<ActionStep>();

    public bool CanUse => !IsRecording && Events.Count > 0;

    public override Task OnNavigatedToAsync()
    {
        RecordMouseMoves = _library.Settings.RecordMouseMoves;
        return Task.CompletedTask;
    }

    public void Toggle()
    {
        if (IsRecording) Stop();
        else Start();
    }

    [RelayCommand]
    private void Start()
    {
        if (IsRecording) return;
        try
        {
            Events.Clear();
            _captured = Array.Empty<ActionStep>();
            _recorder.Start(RecordMouseMoves);
            _startedAt = DateTime.Now;
            Elapsed = TimeSpan.Zero;
            IsRecording = true;
            _clock.Start();
            StatusMessage = "녹화 중… 중지 핫키(F10) 또는 ClickFlow 창의 [중지] 버튼으로 종료합니다.";
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }
        catch (Exception ex)
        {
            _dialogs.Error("녹화 시작 실패", ex.Message);
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (!IsRecording) return;
        _clock.Stop();
        _captured = _recorder.Stop();
        IsRecording = false;
        OnPropertyChanged(nameof(CanUse));
        StatusMessage = $"{_captured.Count}개 동작이 기록되었습니다.";
        var main = Application.Current.MainWindow;
        main.WindowState = WindowState.Normal;
        main.Activate();
    }

    [RelayCommand]
    private void Clear()
    {
        Events.Clear();
        _captured = Array.Empty<ActionStep>();
        OnPropertyChanged(nameof(CanUse));
    }

    [RelayCommand]
    private async Task SaveAsNewAsync()
    {
        if (_captured.Count == 0) return;
        var macro = new Macro
        {
            Name = string.IsNullOrWhiteSpace(NewMacroName) ? $"녹화 {DateTime.Now:MM-dd HH:mm}" : NewMacroName.Trim(),
            Actions = _captured.Select(a => a.Clone()).ToList(),
        };
        await _library.SaveAsync(macro);
        _navigation.OpenMacro(macro);
    }

    [RelayCommand]
    private void AppendToEditor()
    {
        if (_captured.Count == 0) return;
        _editor.AppendRecorded(_captured.Select(a => a.Clone()));
        _navigation.NavigateTo(PageKind.Editor);
    }

    private void OnEventCaptured(object? sender, RecordedEvent e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Events.Add(e);
            OnPropertyChanged(nameof(CanUse));
        });
    }
}
