using System.Collections.ObjectModel;
using System.Windows.Threading;
using ClickFlow.App.Services;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class DashboardViewModel : PageViewModel
{
    private readonly MacroLibrary _library;
    private readonly ExecutionService _execution;
    private readonly INavigationService _navigation;
    private readonly DispatcherTimer _clock;

    public DashboardViewModel(MacroLibrary library, ExecutionService execution, INavigationService navigation)
    {
        _library = library;
        _execution = execution;
        _navigation = navigation;
        _library.Changed += (_, _) => Refresh();
        _execution.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ExecutionService.State) or nameof(ExecutionService.LastResult)) Refresh();
        };
        _clock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => OnPropertyChanged(nameof(Now));
    }

    public override string Title => "대시보드";

    public ExecutionService Execution => _execution;

    public ObservableCollection<Macro> Upcoming { get; } = new();
    public ObservableCollection<Macro> Recent { get; } = new();

    [ObservableProperty]
    private int _macroCount;

    [ObservableProperty]
    private int _scheduledCount;

    [ObservableProperty]
    private int _runCount;

    [ObservableProperty]
    private int _successCount;

    public DateTime Now => DateTime.Now;

    public override Task OnNavigatedToAsync()
    {
        Refresh();
        _clock.Start();
        return Task.CompletedTask;
    }

    public override void OnNavigatedFrom() => _clock.Stop();

    [RelayCommand]
    private void Open(Macro? macro)
    {
        if (macro is not null) _navigation.OpenMacro(macro);
    }

    [RelayCommand]
    private async Task RunAsync(Macro? macro)
    {
        if (macro is null || _execution.IsBusy) return;
        _navigation.NavigateTo(PageKind.Execution);
        await _execution.RunAsync(macro);
    }

    [RelayCommand]
    private void GoToRecorder() => _navigation.NavigateTo(PageKind.Recorder);

    [RelayCommand]
    private void GoToMacros() => _navigation.NavigateTo(PageKind.Macros);

    private void Refresh()
    {
        MacroCount = _library.Macros.Count;

        var now = DateTime.Now;
        var scheduled = _library.Macros
            .Where(m => m.Schedule.Type == ScheduleType.Once && m.Schedule.StartTime > now)
            .OrderBy(m => m.Schedule.StartTime)
            .ToList();
        ScheduledCount = scheduled.Count;
        Upcoming.Clear();
        foreach (var macro in scheduled.Take(5)) Upcoming.Add(macro);

        Recent.Clear();
        foreach (var macro in _library.Macros.OrderByDescending(m => m.UpdatedAt).Take(6)) Recent.Add(macro);

        RunCount = _execution.History.Count;
        SuccessCount = _execution.History.Count(r => r.Succeeded);
    }
}
