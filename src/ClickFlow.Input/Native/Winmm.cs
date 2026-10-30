using System.Runtime.InteropServices;

namespace ClickFlow.Input.Native;

internal static partial class Winmm
{
    private const string Lib = "winmm.dll";

    public const uint TIMERR_NOERROR = 0;

    [DllImport(Lib)]
    public static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport(Lib)]
    public static extern uint timeEndPeriod(uint uPeriod);
}
