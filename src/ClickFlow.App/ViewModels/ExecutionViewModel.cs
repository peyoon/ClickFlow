using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using ClickFlow.App.Services;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class ExecutionViewModel : PageViewModel
{
    private readonly ExecutionService _execution;
    private readonly MacroLibrary _library;
    private readonly IDialogService _dialogs;

    public ExecutionViewModel(ExecutionService execution, MacroLibrary library, IDialogService dialogs)
    {
        _execution = execution;
        _library = library;
        _dialogs = dialogs;
        _execution.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ExecutionService.CurrentMacro):
                    RebuildActions();
                    break;
                case nameof(ExecutionService.CurrentActionIndex):
                    foreach (var a in Actions) a.IsCurrent = a.Index == _execution.CurrentActionIndex;
                    break;
                case nameof(ExecutionService.State):
                    OnPropertyChanged(nameof(IsIdle));
                    OnPropertyChanged(nameof(ScheduledTime));
                    break;
            }
        };
        _execution.Log.CollectionChanged += OnLogChanged;
    }

    public override string Title => "실행";

    public ExecutionService Execution => _execution;
    public ObservableCollection<Macro> Macros => _library.Macros;
    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();
    public bool IsIdle => !_execution.IsBusy;
    public DateTime? ScheduledTime => _execution.State == ExecutionState.Waiting ? _execution.CurrentMacro?.Schedule.StartTime : null;

    [ObservableProperty]
    private Macro? _selectedMacro;

    [ObservableProperty]
    private bool _autoScroll = true;

    public event EventHandler? LogAppended;

    [RelayCommand]
    private async Task StartAsync()
    {
        var macro = SelectedMacro ?? _execution.CurrentMacro;
        if (macro is null || _execution.IsBusy) return;
        await _execution.RunAsync(macro);
    }

    [RelayCommand]
    private void Pause() => _execution.TogglePause();

    [RelayCommand]
    private void Stop() => _execution.Stop();

    [RelayCommand]
    private void ClearLog() => _execution.Log.Clear();

    [RelayCommand]
    private void CopyLog()
    {
        var sb = new StringBuilder();
        foreach (var entry in _execution.Log) sb.AppendLine(entry.ToString());
        System.Windows.Clipboard.SetText(sb.ToString());
    }

    [RelayCommand]
    private void SaveLog()
    {
        var path = _dialogs.SaveFile("로그 파일 (*.log)|*.log|텍스트 (*.txt)|*.txt", $"clickflow_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        if (path is null) return;
        File.WriteAllLines(path, _execution.Log.Select(e => e.ToString()), Encoding.UTF8);
    }

    private void RebuildActions()
    {
        Actions.Clear();
        var macro = _execution.CurrentMacro;
        if (macro is null) return;
        for (var i = 0; i < macro.Actions.Count; i++)
        {
            Actions.Add(new ActionItemViewModel(macro.Actions[i], () => { }) { Index = i });
        }
        if (SelectedMacro is null || SelectedMacro.Id != macro.Id) SelectedMacro = _library.Find(macro.Id) ?? macro;
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && AutoScroll) LogAppended?.Invoke(this, EventArgs.Empty);
    }
}
