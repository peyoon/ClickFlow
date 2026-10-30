using System.Runtime.InteropServices;

namespace ClickFlow.Input.Native;

internal static partial class Gdi32
{
    private const string Lib = "gdi32.dll";

    public const uint CLR_INVALID = 0xFFFFFFFF;

    [DllImport(Lib)]
    public static extern uint GetPixel(nint hdc, int x, int y);
}
