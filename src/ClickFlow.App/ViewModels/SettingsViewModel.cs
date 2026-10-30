using ClickFlow.App.Services;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly MacroLibrary _library;
    private readonly IDialogService _dialogs;
    private readonly HotkeyCoordinator _hotkeys;

    public SettingsViewModel(MacroLibrary library, IDialogService dialogs, HotkeyCoordinator hotkeys)
    {
        _library = library;
        _dialogs = dialogs;
        _hotkeys = hotkeys;
    }

    public override string Title => "설정";

    public AppSettings Settings => _library.Settings;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string MacroDirectory
    {
        get => Settings.MacroDirectory;
        set { Settings.MacroDirectory = value; OnPropertyChanged(); }
    }

    public string ScreenshotDirectory
    {
        get => Settings.ScreenshotDirectory;
        set { Settings.ScreenshotDirectory = value; OnPropertyChanged(); }
    }

    public string StartHotkey
    {
        get => Settings.StartHotkey;
        set { Settings.StartHotkey = value; OnPropertyChanged(); }
    }

    public string PauseHotkey
    {
        get => Settings.PauseHotkey;
        set { Settings.PauseHotkey = value; OnPropertyChanged(); }
    }

    public string StopHotkey
    {
        get => Settings.StopHotkey;
        set { Settings.StopHotkey = value; OnPropertyChanged(); }
    }

    public string PanicHotkey
    {
        get => Settings.PanicHotkey;
        set { Settings.PanicHotkey = value; OnPropertyChanged(); }
    }

    public bool PanicOnCornerMove
    {
        get => Settings.PanicOnCornerMove;
        set { Settings.PanicOnCornerMove = value; OnPropertyChanged(); }
    }

    public bool MinimizeToTray
    {
        get => Settings.MinimizeToTray;
        set { Settings.MinimizeToTray = value; OnPropertyChanged(); }
    }

    public int HighResolutionWindowMs
    {
        get => Settings.HighResolutionWindowMs;
        set { Settings.HighResolutionWindowMs = Math.Clamp(value, 10, 2000); OnPropertyChanged(); }
    }

    public bool RecordMouseMoves
    {
        get => Settings.RecordMouseMoves;
        set { Settings.RecordMouseMoves = value; OnPropertyChanged(); }
    }

    public override Task OnNavigatedToAsync()
    {
        RaiseAll();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        foreach (var (label, key) in new[] { ("시작", StartHotkey), ("일시정지", PauseHotkey), ("중지", StopHotkey), ("비상 정지", PanicHotkey) })
        {
            if (!GlobalHotkeyService.TryParse(key, out _, out _))
            {
                StatusMessage = $"{label} 핫키 '{key}'를 인식할 수 없습니다. 예: F8, Ctrl+Shift+F9";
                return;
            }
        }

        try
        {
            await _library.SaveSettingsAsync();
            var failed = _hotkeys.Apply();
            StatusMessage = failed.Count == 0
                ? $"저장됨 · {DateTime.Now:HH:mm:ss}"
                : $"저장됨. 다음 핫키는 다른 프로그램이 사용 중입니다: {string.Join(", ", failed)}";
        }
        catch (Exception ex)
        {
            _dialogs.Error("설정 저장 실패", ex.Message);
        }
    }

    [RelayCommand]
    private void BrowseMacroDirectory()
    {
        var folder = _dialogs.PickFolder(MacroDirectory);
        if (folder is not null) MacroDirectory = folder;
    }

    [RelayCommand]
    private void BrowseScreenshotDirectory()
    {
        var folder = _dialogs.PickFolder(ScreenshotDirectory);
        if (folder is not null) ScreenshotDirectory = folder;
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Storage.AppPaths.EnsureRootDirectory()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.Error("폴더 열기 실패", ex.Message);
        }
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(MacroDirectory), nameof(ScreenshotDirectory), nameof(StartHotkey), nameof(PauseHotkey), nameof(StopHotkey),
                     nameof(PanicHotkey), nameof(PanicOnCornerMove), nameof(MinimizeToTray), nameof(HighResolutionWindowMs), nameof(RecordMouseMoves),
                 })
        {
            OnPropertyChanged(name);
        }
    }
}
