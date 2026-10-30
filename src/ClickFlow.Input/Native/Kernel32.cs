using System.Runtime.InteropServices;

namespace ClickFlow.Input.Native;

internal static partial class Kernel32
{
    private const string Lib = "kernel32.dll";

    [DllImport(Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport(Lib)]
    public static extern uint GetCurrentThreadId();
}
