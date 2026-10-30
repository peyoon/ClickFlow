using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClickFlow.Core;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Input.Native;
using static ClickFlow.Input.Native.User32;

namespace ClickFlow.Input;

[SupportedOSPlatform("windows")]
public sealed class WindowsInputDriver : IInputDriver
{
    private const int MultiClickIntervalMs = 50;
    private const int PostMovePauseMs = 5;

    private static readonly object TimerLock = new();
    private static bool _timerResolutionRaised;

    private static readonly HashSet<ushort> ExtendedKeys = new()
    {
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28,
        0x2C, 0x2D, 0x2E,
        0x5B, 0x5C, 0x5D,
        0x6F, 0x90, 0x13,
        0xA3, 0xA5,
    };

    public static void EnsureTimerResolution()
    {
        lock (TimerLock)
        {
            if (_timerResolutionRaised) return;
            if (Winmm.timeBeginPeriod(1) != Winmm.TIMERR_NOERROR) return;
            _timerResolutionRaised = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Winmm.timeEndPeriod(1);
        }
    }

    public ScreenPoint GetCursorPosition()
    {
        if (!GetCursorPos(out var pt))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return new ScreenPoint(pt.X, pt.Y);
    }

    public void MoveTo(int x, int y)
    {
        Send(new[] { BuildAbsoluteMove(x, y) });
        SetCursorPos(x, y);
    }

    public void MouseDown(MouseButton button) =>
        Send(new[] { BuildMouse(DownFlag(button)) });

    public void MouseUp(MouseButton button) =>
        Send(new[] { BuildMouse(UpFlag(button)) });

    public void Click(int x, int y, MouseButton button, int clickCount, bool moveFirst)
    {
        if (moveFirst)
        {
            MoveTo(x, y);
            Thread.Sleep(PostMovePauseMs);
        }

        var count = Math.Max(1, clickCount);
        var pair = new[] { BuildMouse(DownFlag(button)), BuildMouse(UpFlag(button)) };
        for (var i = 0; i < count; i++)
        {
            if (i > 0) Thread.Sleep(MultiClickIntervalMs);
            Send(pair);
        }
    }

    public void Scroll(int delta) =>
        Send(new[] { BuildMouse(MOUSEEVENTF_WHEEL, mouseData: unchecked((uint)delta)) });

    public void KeyDown(string key, KeyModifiers modifiers)
    {
        var inputs = new List<INPUT>(5);
        AppendModifiers(inputs, modifiers, keyUp: false);
        AppendKey(inputs, key, keyUp: false);
        Send(inputs);
    }

    public void KeyUp(string key, KeyModifiers modifiers)
    {
        var inputs = new List<INPUT>(5);
        AppendKey(inputs, key, keyUp: true);
        AppendModifiers(inputs, modifiers, keyUp: true);
        Send(inputs);
    }

    public void KeyPress(string key, KeyModifiers modifiers)
    {
        var inputs = new List<INPUT>(10);
        AppendModifiers(inputs, modifiers, keyUp: false);
        AppendKey(inputs, key, keyUp: false);
        AppendKey(inputs, key, keyUp: true);
        AppendModifiers(inputs, modifiers, keyUp: true);
        Send(inputs);
    }

    public void TypeText(string text, int intervalMs, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text)) return;

        var first = true;
        foreach (var ch in text)
        {
            ct.ThrowIfCancellationRequested();
            if (!first && intervalMs > 0) Thread.Sleep(intervalMs);
            first = false;

            if (ch == '\r') continue;

            if (ch == '\n')
            {
                Send(new[] { BuildVirtualKey(VK_RETURN, keyUp: false), BuildVirtualKey(VK_RETURN, keyUp: true) });
                continue;
            }

            Send(new[] { BuildUnicode(ch, keyUp: false), BuildUnicode(ch, keyUp: true) });
        }
    }

    private static void AppendModifiers(List<INPUT> inputs, KeyModifiers modifiers, bool keyUp)
    {
        if (modifiers == KeyModifiers.None) return;

        Span<ushort> order = stackalloc ushort[4];
        var n = 0;
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) order[n++] = VK_CONTROL;
        if (modifiers.HasFlag(KeyModifiers.Shift)) order[n++] = VK_SHIFT;
        if (modifiers.HasFlag(KeyModifiers.Alt)) order[n++] = VK_MENU;
        if (modifiers.HasFlag(KeyModifiers.Win)) order[n++] = VK_LWIN;

        if (keyUp)
        {
            for (var i = n - 1; i >= 0; i--) inputs.Add(BuildVirtualKey(order[i], keyUp: true));
        }
        else
        {
            for (var i = 0; i < n; i++) inputs.Add(BuildVirtualKey(order[i], keyUp: false));
        }
    }

    private static void AppendKey(List<INPUT> inputs, string key, bool keyUp)
    {
        if (VirtualKeys.TryGetCode(key, out var vk))
        {
            inputs.Add(BuildVirtualKey(vk, keyUp));
            return;
        }

        var trimmed = key.Trim();
        if (trimmed.Length == 1)
        {
            inputs.Add(BuildUnicode(trimmed[0], keyUp));
            return;
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            ushort.TryParse(trimmed.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hexVk))
        {
            inputs.Add(BuildVirtualKey(hexVk, keyUp));
            return;
        }

        throw new ArgumentException($"알 수 없는 키 이름: '{key}'", nameof(key));
    }

    private static INPUT BuildVirtualKey(ushort vk, bool keyUp)
    {
        var scan = (ushort)MapVirtualKeyW(vk, MAPVK_VK_TO_VSC);
        uint flags = 0;
        if (scan != 0) flags |= KEYEVENTF_SCANCODE;
        if (ExtendedKeys.Contains(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
        if (keyUp) flags |= KEYEVENTF_KEYUP;

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                },
            },
        };
    }

    private static INPUT BuildUnicode(char ch, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
            },
        },
    };

    private static INPUT BuildMouse(uint flags, int dx = 0, int dy = 0, uint mouseData = 0) => new()
    {
        type = INPUT_MOUSE,
        u = new INPUTUNION
        {
            mi = new MOUSEINPUT
            {
                dx = dx,
                dy = dy,
                mouseData = mouseData,
                dwFlags = flags,
            },
        },
    };

    private static INPUT BuildAbsoluteMove(int x, int y)
    {
        var originX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var originY = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
        var height = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));

        var nx = (int)(((long)(x - originX) * 65536 + width / 2) / width);
        var ny = (int)(((long)(y - originY) * 65536 + height / 2) / height);

        return BuildMouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, nx, ny);
    }

    private static uint DownFlag(MouseButton button) => button switch
    {
        MouseButton.Left => MOUSEEVENTF_LEFTDOWN,
        MouseButton.Right => MOUSEEVENTF_RIGHTDOWN,
        MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
    };

    private static uint UpFlag(MouseButton button) => button switch
    {
        MouseButton.Left => MOUSEEVENTF_LEFTUP,
        MouseButton.Right => MOUSEEVENTF_RIGHTUP,
        MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
    };

    private static void Send(List<INPUT> inputs) => Send(inputs.ToArray());

    private static void Send(INPUT[] inputs)
    {
        if (inputs.Length == 0) return;
        var sent = SendInput((uint)inputs.Length, inputs, INPUT.Size);
        if (sent != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput sent {sent}/{inputs.Length} events.");
    }
}
