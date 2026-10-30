using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Tests.Fakes;

public sealed class FakeWindowService : IWindowService
{
    public List<WindowInfo> Windows { get; } = new();

    public WindowInfo? Foreground { get; set; }

    public List<nint> BroughtToFront { get; } = new();

    public WindowInfo? GetForegroundWindow() => Foreground;

    public WindowInfo? FindWindow(string? titleContains, string? processName)
    {
        return Windows.FirstOrDefault(w =>
            (titleContains is null || w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            && (processName is null || string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase)));
    }

    public IReadOnlyList<WindowInfo> EnumerateWindows() => Windows;

    public bool BringToFront(nint handle)
    {
        BroughtToFront.Add(handle);
        return true;
    }

    public ScreenRegion GetVirtualScreenBounds() => new() { X = 0, Y = 0, Width = 1920, Height = 1080 };
}
