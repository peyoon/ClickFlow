using System.Diagnostics;
using System.Runtime.Versioning;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Input.Native;

namespace ClickFlow.Input;

[SupportedOSPlatform("windows")]
public sealed class WindowService : IWindowService
{
    public WindowInfo? GetForegroundWindow()
    {
        var handle = User32.GetForegroundWindow();
        return handle == 0 ? null : Describe(handle);
    }

    public WindowInfo? FindWindow(string? titleContains, string? processName)
    {
        var title = string.IsNullOrWhiteSpace(titleContains) ? null : titleContains.Trim();
        var process = NormalizeProcessName(processName);
        if (title is null && process is null) return null;

        WindowInfo? found = null;
        User32.EnumWindows((hWnd, _) =>
        {
            var info = DescribeIfVisibleTopLevel(hWnd);
            if (info is null) return true;

            if (title is not null && !info.Title.Contains(title, StringComparison.OrdinalIgnoreCase)) return true;
            if (process is not null && !string.Equals(info.ProcessName, process, StringComparison.OrdinalIgnoreCase)) return true;

            found = info;
            return false;
        }, 0);

        return found;
    }

    public IReadOnlyList<WindowInfo> EnumerateWindows()
    {
        var result = new List<WindowInfo>();
        User32.EnumWindows((hWnd, _) =>
        {
            var info = DescribeIfVisibleTopLevel(hWnd);
            if (info is not null) result.Add(info);
            return true;
        }, 0);
        return result;
    }

    public bool BringToFront(nint handle)
    {
        if (handle == 0) return false;

        if (User32.IsIconic(handle))
            User32.ShowWindow(handle, User32.SW_RESTORE);

        if (User32.SetForegroundWindow(handle) && User32.GetForegroundWindow() == handle)
            return true;

        var foreground = User32.GetForegroundWindow();
        var foregroundThread = foreground == 0 ? 0 : User32.GetWindowThreadProcessId(foreground, out _);
        var targetThread = User32.GetWindowThreadProcessId(handle, out _);
        var currentThread = Kernel32.GetCurrentThreadId();

        var attachedForeground = foregroundThread != 0 && foregroundThread != currentThread &&
                                 User32.AttachThreadInput(currentThread, foregroundThread, true);
        var attachedTarget = targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread &&
                             User32.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            User32.SetForegroundWindow(handle);
        }
        finally
        {
            if (attachedTarget) User32.AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) User32.AttachThreadInput(currentThread, foregroundThread, false);
        }

        return User32.GetForegroundWindow() == handle;
    }

    public ScreenRegion GetVirtualScreenBounds() => new()
    {
        X = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN),
        Y = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN),
        Width = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN),
        Height = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN),
    };

    private static WindowInfo? DescribeIfVisibleTopLevel(nint hWnd)
    {
        if (!User32.IsWindowVisible(hWnd)) return null;
        var title = User32.GetWindowText(hWnd);
        if (title.Length == 0) return null;
        return Describe(hWnd, title);
    }

    private static WindowInfo Describe(nint hWnd, string? title = null)
    {
        title ??= User32.GetWindowText(hWnd);

        User32.GetWindowThreadProcessId(hWnd, out var pid);
        var processName = GetProcessName(pid);

        var bounds = new ScreenRegion();
        if (User32.GetWindowRect(hWnd, out var rect))
        {
            bounds.X = rect.Left;
            bounds.Y = rect.Top;
            bounds.Width = rect.Width;
            bounds.Height = rect.Height;
        }

        var clientBounds = new ScreenRegion();
        if (User32.GetClientRect(hWnd, out var client))
        {
            var origin = new POINT();
            User32.ClientToScreen(hWnd, ref origin);
            clientBounds.X = origin.X;
            clientBounds.Y = origin.Y;
            clientBounds.Width = client.Width;
            clientBounds.Height = client.Height;
        }

        return new WindowInfo(hWnd, title, processName, bounds, clientBounds);
    }

    private static string GetProcessName(uint pid)
    {
        if (pid == 0) return string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return null;
        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name.Length == 0 ? null : name;
    }
}
