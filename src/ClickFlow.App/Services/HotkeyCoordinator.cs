using System.Windows.Threading;
using ClickFlow.App.ViewModels;
using ClickFlow.Core.Interfaces;

namespace ClickFlow.App.Services;

public sealed class HotkeyCoordinator : IDisposable
{
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly ExecutionService _execution;
    private readonly MacroLibrary _library;
    private readonly IInputDriver _input;
    private readonly DispatcherTimer _cornerWatch;
    private Func<RecorderViewModel>? _recorderAccessor;
    private Func<Core.Models.Macro?>? _macroToStart;

    public HotkeyCoordinator(IGlobalHotkeyService hotkeys, ExecutionService execution, MacroLibrary library, IInputDriver input)
    {
        _hotkeys = hotkeys;
        _execution = execution;
        _library = library;
        _input = input;
        _cornerWatch = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _cornerWatch.Tick += (_, _) => CheckCorner();
    }

    public event EventHandler<string>? PanicTriggered;

    public void Attach(Func<RecorderViewModel> recorderAccessor, Func<Core.Models.Macro?> macroToStart)
    {
        _recorderAccessor = recorderAccessor;
        _macroToStart = macroToStart;
        _cornerWatch.Start();
    }

    public IReadOnlyList<string> Apply()
    {
        var settings = _library.Settings;
        var failed = new List<string>();
        _hotkeys.UnregisterAll();

        if (!_hotkeys.Register("start", settings.StartHotkey, OnStart)) failed.Add(settings.StartHotkey);
        if (!_hotkeys.Register("pause", settings.PauseHotkey, () => _execution.TogglePause())) failed.Add(settings.PauseHotkey);
        if (!_hotkeys.Register("stop", settings.StopHotkey, OnStop)) failed.Add(settings.StopHotkey);
        if (!_hotkeys.Register("panic", settings.PanicHotkey, () => Panic($"비상 정지 핫키 ({settings.PanicHotkey})"))) failed.Add(settings.PanicHotkey);
        return failed;
    }

    private void OnStart()
    {
        if (_recorderAccessor?.Invoke() is { IsRecording: true }) return;
        if (_execution.IsBusy) return;
        var macro = _macroToStart?.Invoke();
        if (macro is not null) _ = _execution.RunAsync(macro);
    }

    private void OnStop()
    {
        var recorder = _recorderAccessor?.Invoke();
        if (recorder is { IsRecording: true })
        {
            recorder.Toggle();
            return;
        }
        _execution.Stop();
    }

    private void CheckCorner()
    {
        if (!_library.Settings.PanicOnCornerMove || !_execution.IsBusy) return;
        var pos = _input.GetCursorPosition();
        if (pos.X <= 0 && pos.Y <= 0) Panic("마우스가 화면 왼쪽 위 모서리로 이동");
    }

    private void Panic(string reason)
    {
        _execution.Stop();
        if (_recorderAccessor?.Invoke() is { IsRecording: true } recorder) recorder.Toggle();
        PanicTriggered?.Invoke(this, reason);
    }

    public void Dispose()
    {
        _cornerWatch.Stop();
        _hotkeys.Dispose();
    }
}
