namespace ClickFlow.Core;

public static class VirtualKeys
{
    private static readonly Dictionary<string, ushort> Map = BuildMap();

    public static IReadOnlyCollection<string> Names => Map.Keys;

    public static bool TryGetCode(string name, out ushort code) =>
        Map.TryGetValue(Normalize(name), out code);

    public static ushort GetCode(string name) =>
        TryGetCode(name, out var code)
            ? code
            : throw new ArgumentException($"알 수 없는 키 이름: '{name}'", nameof(name));

    public static string? GetName(ushort code)
    {
        foreach (var (name, value) in Map)
        {
            if (value == code) return name;
        }
        return null;
    }

    public static string Normalize(string name)
    {
        var upper = name.Trim().ToUpperInvariant();
        return upper switch
        {
            "RETURN" => "ENTER",
            "ESC" => "ESCAPE",
            "DEL" => "DELETE",
            "INS" => "INSERT",
            "PGUP" => "PAGEUP",
            "PGDN" or "PGDOWN" => "PAGEDOWN",
            "CONTROL" => "CTRL",
            "WINDOWS" or "LWIN" => "WIN",
            "BACK" or "BKSP" => "BACKSPACE",
            " " => "SPACE",
            _ => upper,
        };
    }

    private static Dictionary<string, ushort> BuildMap()
    {
        var map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["BACKSPACE"] = 0x08,
            ["TAB"] = 0x09,
            ["ENTER"] = 0x0D,
            ["SHIFT"] = 0x10,
            ["CTRL"] = 0x11,
            ["ALT"] = 0x12,
            ["PAUSE"] = 0x13,
            ["CAPSLOCK"] = 0x14,
            ["HANGUL"] = 0x15,
            ["HANJA"] = 0x19,
            ["ESCAPE"] = 0x1B,
            ["SPACE"] = 0x20,
            ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22,
            ["END"] = 0x23,
            ["HOME"] = 0x24,
            ["LEFT"] = 0x25,
            ["UP"] = 0x26,
            ["RIGHT"] = 0x27,
            ["DOWN"] = 0x28,
            ["PRINTSCREEN"] = 0x2C,
            ["INSERT"] = 0x2D,
            ["DELETE"] = 0x2E,
            ["WIN"] = 0x5B,
            ["RWIN"] = 0x5C,
            ["APPS"] = 0x5D,
            ["NUMLOCK"] = 0x90,
            ["SCROLLLOCK"] = 0x91,
            ["MULTIPLY"] = 0x6A,
            ["ADD"] = 0x6B,
            ["SUBTRACT"] = 0x6D,
            ["DECIMAL"] = 0x6E,
            ["DIVIDE"] = 0x6F,
            ["SEMICOLON"] = 0xBA,
            ["EQUALS"] = 0xBB,
            ["COMMA"] = 0xBC,
            ["MINUS"] = 0xBD,
            ["PERIOD"] = 0xBE,
            ["SLASH"] = 0xBF,
            ["BACKQUOTE"] = 0xC0,
            ["LBRACKET"] = 0xDB,
            ["BACKSLASH"] = 0xDC,
            ["RBRACKET"] = 0xDD,
            ["QUOTE"] = 0xDE,
        };

        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = (ushort)c;
        for (var d = 0; d <= 9; d++) map[d.ToString()] = (ushort)('0' + d);
        for (var n = 0; n <= 9; n++) map[$"NUMPAD{n}"] = (ushort)(0x60 + n);
        for (var f = 1; f <= 24; f++) map[$"F{f}"] = (ushort)(0x70 + f - 1);

        return map;
    }
}
