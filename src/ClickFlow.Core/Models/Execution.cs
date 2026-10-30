namespace ClickFlow.Core.Models;

public sealed record ExecutionLogEntry(DateTime Timestamp, LogLevel Level, string Message, int? ActionIndex = null)
{
    public override string ToString() =>
        ActionIndex.HasValue
            ? $"[{Timestamp:HH:mm:ss.fff}] #{ActionIndex + 1} {Message}"
            : $"[{Timestamp:HH:mm:ss.fff}] {Message}";
}

public sealed class ExecutionResult
{
    public Guid MacroId { get; init; }
    public string MacroName { get; init; } = string.Empty;
    public ExecutionState FinalState { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public int ExecutedActionCount { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ExecutionLogEntry> Log { get; init; } = Array.Empty<ExecutionLogEntry>();

    public TimeSpan Duration => EndedAt - StartedAt;
    public bool Succeeded => FinalState == ExecutionState.Completed;
}

public sealed record ScreenPoint(int X, int Y);

public sealed record WindowInfo(nint Handle, string Title, string ProcessName, ScreenRegion Bounds, ScreenRegion ClientBounds);

public sealed record RecordedEvent(TimeSpan Offset, ActionStep Action);

public sealed class AppSettings
{
    public string MacroDirectory { get; set; } = string.Empty;
    public string ScreenshotDirectory { get; set; } = string.Empty;
    public string StartHotkey { get; set; } = "F8";
    public string PauseHotkey { get; set; } = "F9";
    public string StopHotkey { get; set; } = "F10";
    public string PanicHotkey { get; set; } = "F12";
    public bool PanicOnCornerMove { get; set; } = true;
    public bool DarkMode { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public int HighResolutionWindowMs { get; set; } = 100;
    public bool RecordMouseMoves { get; set; }
}
