using System.Runtime.InteropServices;
using System.Windows.Interop;
using ClickFlow.Core;
using ClickFlow.Core.Interfaces;

namespace ClickFlow.App.Services;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly HwndSource _source;
    private readonly Dictionary<string, (int AtomId, Action Callback)> _registrations = new();
    private int _nextAtomId = 0xC000;

    public GlobalHotkeyService()
    {
        var parameters = new HwndSourceParameters("ClickFlowHotkeySink")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3),
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool Register(string id, string key, Action callback)
    {
        Unregister(id);
        if (!TryParse(key, out var modifiers, out var vk)) return false;

        var atomId = _nextAtomId++;
        if (!RegisterHotKey(_source.Handle, atomId, modifiers | ModNoRepeat, vk)) return false;

        _registrations[id] = (atomId, callback);
        return true;
    }

    public void Unregister(string id)
    {
        if (!_registrations.Remove(id, out var entry)) return;
        UnregisterHotKey(_source.Handle, entry.AtomId);
    }

    public void UnregisterAll()
    {
        foreach (var id in _registrations.Keys.ToList()) Unregister(id);
    }

    public static bool TryParse(string key, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;

        var parts = key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (VirtualKeys.Normalize(parts[i]))
            {
                case "CTRL": modifiers |= ModControl; break;
                case "SHIFT": modifiers |= ModShift; break;
                case "ALT": modifiers |= ModAlt; break;
                case "WIN": modifiers |= ModWin; break;
                default: return false;
            }
        }

        if (!VirtualKeys.TryGetCode(parts[^1], out var code)) return false;
        vk = code;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;

        var atomId = wParam.ToInt32();
        foreach (var (_, entry) in _registrations)
        {
            if (entry.AtomId != atomId) continue;
            entry.Callback();
            handled = true;
            break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
