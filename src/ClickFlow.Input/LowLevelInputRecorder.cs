using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClickFlow.Core;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Input.Native;
using static ClickFlow.Input.Native.User32;

namespace ClickFlow.Input;

[SupportedOSPlatform("windows")]
public sealed class LowLevelInputRecorder : IInputRecorder, IDisposable
{
    private const int MinGapForDelayMs = 20;
    private const int MouseMoveMinIntervalMs = 50;
    private const int MouseMoveMinDistancePx = 8;
    private const int DoubleClickMaxDistancePx = 4;

    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private readonly object _lifecycleLock = new();

    private readonly List<ActionStep> _actions = new();
    private readonly HashSet<ushort> _downKeys = new();
    private readonly Stopwatch _clock = new();

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _mouseHook;
    private nint _keyboardHook;
    private Exception? _startupError;
    private volatile bool _isRecording;

    private bool _recordMouseMoves;
    private long _lastEmitMs;
    private long _lastMoveMs;
    private int _lastMoveX;
    private int _lastMoveY;
    private bool _hasLastMove;

    private MouseButton? _pendingButton;
    private MouseClickAction? _lastClick;
    private long _lastClickMs;

    public LowLevelInputRecorder()
    {
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public bool IsRecording => _isRecording;

    public event EventHandler<RecordedEvent>? EventCaptured;

    public void Start(bool recordMouseMoves)
    {
        lock (_lifecycleLock)
        {
            if (_isRecording)
                throw new InvalidOperationException("Recording is already in progress.");

            ResetState(recordMouseMoves);

            using var ready = new ManualResetEventSlim(false);
            _hookThread = new Thread(() => HookThreadMain(ready))
            {
                Name = "ClickFlow.LowLevelInputRecorder",
                IsBackground = true,
            };
            _hookThread.Start();
            ready.Wait();

            if (_startupError is not null)
            {
                _hookThread.Join();
                _hookThread = null;
                var error = _startupError;
                _startupError = null;
                throw error;
            }

            _clock.Restart();
            _isRecording = true;
        }
    }

    public IReadOnlyList<ActionStep> Stop()
    {
        lock (_lifecycleLock)
        {
            if (!_isRecording) return _actions.ToArray();

            _isRecording = false;
            if (_hookThreadId != 0)
                PostThreadMessageW(_hookThreadId, WM_QUIT, 0, 0);

            _hookThread?.Join();
            _hookThread = null;
            _hookThreadId = 0;
            _clock.Stop();

            return _actions.ToArray();
        }
    }

    public void Dispose()
    {
        if (_isRecording) Stop();
    }

    private void ResetState(bool recordMouseMoves)
    {
        _recordMouseMoves = recordMouseMoves;
        _actions.Clear();
        _downKeys.Clear();
        _lastEmitMs = 0;
        _lastMoveMs = 0;
        _hasLastMove = false;
        _pendingButton = null;
        _lastClick = null;
        _lastClickMs = 0;
        _startupError = null;
    }

    private void HookThreadMain(ManualResetEventSlim ready)
    {
        _hookThreadId = Kernel32.GetCurrentThreadId();
        PeekMessageW(out _, 0, 0, 0, 0);

        var module = Kernel32.GetModuleHandleW(null);
        _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, _mouseProc, module, 0);
        if (_mouseHook == 0)
        {
            _startupError = new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_MOUSE_LL) failed.");
            ready.Set();
            return;
        }

        _keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (_keyboardHook == 0)
        {
            _startupError = new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_KEYBOARD_LL) failed.");
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
            ready.Set();
            return;
        }

        ready.Set();

        try
        {
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }
        finally
        {
            if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
            if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
            _keyboardHook = 0;
            _mouseHook = 0;
        }
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode == HC_ACTION && _isRecording)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            HandleMouseMessage((uint)wParam, data);
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode == HC_ACTION && _isRecording)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            HandleKeyboardMessage((uint)wParam, data);
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void HandleMouseMessage(uint message, in MSLLHOOKSTRUCT data)
    {
        switch (message)
        {
            case WM_LBUTTONDOWN:
                _pendingButton = MouseButton.Left;
                break;
            case WM_RBUTTONDOWN:
                _pendingButton = MouseButton.Right;
                break;
            case WM_MBUTTONDOWN:
                _pendingButton = MouseButton.Middle;
                break;

            case WM_LBUTTONUP:
                CompleteClick(MouseButton.Left, data.pt);
                break;
            case WM_RBUTTONUP:
                CompleteClick(MouseButton.Right, data.pt);
                break;
            case WM_MBUTTONUP:
                CompleteClick(MouseButton.Middle, data.pt);
                break;

            case WM_MOUSEWHEEL:
                Emit(new MouseScrollAction { Delta = (short)((data.mouseData >> 16) & 0xFFFF) });
                break;

            case WM_MOUSEMOVE:
                HandleMouseMove(data.pt);
                break;
        }
    }

    private void CompleteClick(MouseButton button, POINT at)
    {
        if (_pendingButton != button) return;
        _pendingButton = null;

        var now = _clock.ElapsedMilliseconds;

        if (_lastClick is not null &&
            _lastClick.Button == button &&
            _lastClick.ClickCount == 1 &&
            now - _lastClickMs <= GetDoubleClickTime() &&
            Math.Abs(_lastClick.X - at.X) <= DoubleClickMaxDistancePx &&
            Math.Abs(_lastClick.Y - at.Y) <= DoubleClickMaxDistancePx)
        {
            _lastClick.ClickCount = 2;
            _lastClickMs = now;
            _lastEmitMs = now;
            return;
        }

        var click = new MouseClickAction
        {
            X = at.X,
            Y = at.Y,
            Button = button,
            ClickCount = 1,
            MoveBeforeClick = true,
        };
        Emit(click);
        _lastClick = click;
        _lastClickMs = now;
    }

    private void HandleMouseMove(POINT at)
    {
        if (!_recordMouseMoves) return;

        var now = _clock.ElapsedMilliseconds;
        if (_hasLastMove)
        {
            if (now - _lastMoveMs < MouseMoveMinIntervalMs) return;
            if (Math.Abs(at.X - _lastMoveX) < MouseMoveMinDistancePx &&
                Math.Abs(at.Y - _lastMoveY) < MouseMoveMinDistancePx) return;
        }

        _hasLastMove = true;
        _lastMoveMs = now;
        _lastMoveX = at.X;
        _lastMoveY = at.Y;
        Emit(new MouseMoveAction { X = at.X, Y = at.Y });
    }

    private void HandleKeyboardMessage(uint message, in KBDLLHOOKSTRUCT data)
    {
        var vk = (ushort)data.vkCode;

        switch (message)
        {
            case WM_KEYUP:
            case WM_SYSKEYUP:
                _downKeys.Remove(vk);
                return;

            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                break;

            default:
                return;
        }

        if (IsModifier(vk)) return;
        if (!_downKeys.Add(vk)) return;

        Emit(new KeyPressAction
        {
            Key = VirtualKeys.GetName(vk) ?? $"0x{vk:X2}",
            Ctrl = IsKeyDownAsync(VK_CONTROL),
            Shift = IsKeyDownAsync(VK_SHIFT),
            Alt = IsKeyDownAsync(VK_MENU),
            Win = IsKeyDownAsync(VK_LWIN) || IsKeyDownAsync(VK_RWIN),
        });
    }

    private static bool IsModifier(ushort vk) => vk is
        VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN or
        VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or VK_LMENU or VK_RMENU;

    private void Emit(ActionStep action)
    {
        var now = _clock.ElapsedMilliseconds;
        var gap = now - _lastEmitMs;
        if (_actions.Count > 0 && gap >= MinGapForDelayMs)
            action.DelayBeforeMs = (int)Math.Min(gap, int.MaxValue);
        _lastEmitMs = now;

        if (action is not MouseClickAction) _lastClick = null;

        _actions.Add(action);
        EventCaptured?.Invoke(this, new RecordedEvent(TimeSpan.FromMilliseconds(now), action));
    }
}
