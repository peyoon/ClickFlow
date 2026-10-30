using ClickFlow.Core.Models;

namespace ClickFlow.Core.Interfaces;

public interface IInputDriver
{
    ScreenPoint GetCursorPosition();
    void MoveTo(int x, int y);
    void MouseDown(MouseButton button);
    void MouseUp(MouseButton button);
    void Click(int x, int y, MouseButton button, int clickCount, bool moveFirst);
    void Scroll(int delta);
    void KeyDown(string key, KeyModifiers modifiers);
    void KeyUp(string key, KeyModifiers modifiers);
    void KeyPress(string key, KeyModifiers modifiers);
    void TypeText(string text, int intervalMs, CancellationToken ct);
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}

public interface IWindowService
{
    WindowInfo? GetForegroundWindow();
    WindowInfo? FindWindow(string? titleContains, string? processName);
    IReadOnlyList<WindowInfo> EnumerateWindows();
    bool BringToFront(nint handle);
    ScreenRegion GetVirtualScreenBounds();
}

public interface IScreenService
{
    RgbColor GetPixelColor(int x, int y);
    byte[] CapturePng(ScreenRegion? region);
    string SaveScreenshot(string directory, string? fileName, ScreenRegion? region);
    ScreenPoint? FindImage(string imagePath, ScreenRegion? searchRegion, double similarity);
}

public interface IConditionEvaluator
{
    bool Evaluate(Condition condition, IExecutionContext context);
}

public interface IExecutionContext
{
    Macro Macro { get; }
    DateTime StartedAt { get; }
    CancellationToken CancellationToken { get; }
    WindowInfo? ResolvedTargetWindow { get; }
    void Log(LogLevel level, string message, int? actionIndex = null);
    ScreenPoint ResolvePoint(int x, int y, CoordinateMode mode);
    void ThrowIfStopRequested();
    Task WaitIfPausedAsync();
}

public interface IMacroExecutor
{
    ExecutionState State { get; }
    event EventHandler<ExecutionState>? StateChanged;
    event EventHandler<ExecutionLogEntry>? LogEmitted;
    event EventHandler<int>? ActionStarted;
    Task<ExecutionResult> RunAsync(Macro macro, CancellationToken ct = default);
    void Pause();
    void Resume();
    void Stop();
}

public interface IScheduler
{
    TimeSpan? RemainingTime { get; }
    bool IsWaiting { get; }
    event EventHandler<TimeSpan>? Tick;
    Task WaitUntilAsync(DateTime target, CancellationToken ct);
    Task DelayAsync(int milliseconds, CancellationToken ct);
}

public interface IMacroRepository
{
    string Directory { get; }
    Task<IReadOnlyList<Macro>> LoadAllAsync(CancellationToken ct = default);
    Task<Macro?> LoadAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Macro macro, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Macro> ImportAsync(string filePath, CancellationToken ct = default);
    Task ExportAsync(Macro macro, string filePath, CancellationToken ct = default);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}

public interface IInputRecorder
{
    bool IsRecording { get; }
    event EventHandler<RecordedEvent>? EventCaptured;
    void Start(bool recordMouseMoves);
    IReadOnlyList<ActionStep> Stop();
}

public interface IGlobalHotkeyService : IDisposable
{
    bool Register(string id, string key, Action callback);
    void Unregister(string id);
    void UnregisterAll();
}
