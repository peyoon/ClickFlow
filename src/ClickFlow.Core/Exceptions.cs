namespace ClickFlow.Core;

public sealed class MacroStoppedException : OperationCanceledException
{
    public MacroStoppedException() : base("매크로 실행이 중지되었습니다.") { }
}

public sealed class MacroAbortedException : Exception
{
    public MacroAbortedException(string message) : base(message) { }
}

public sealed class MacroCompletedException : Exception
{
    public bool Success { get; }

    public MacroCompletedException(bool success)
        : base(success ? "매크로가 정상 종료되었습니다." : "매크로가 실패로 종료되었습니다.")
    {
        Success = success;
    }
}
