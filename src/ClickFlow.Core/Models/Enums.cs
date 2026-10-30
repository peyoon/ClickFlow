namespace ClickFlow.Core.Models;

public enum MouseButton
{
    Left,
    Right,
    Middle,
}

public enum CoordinateMode
{
    Screen,
    /// <summary>대상 창의 클라이언트 영역 좌상단 기준.</summary>
    Window,
    /// <summary>실행 시점의 현재 마우스 위치 기준 오프셋.</summary>
    Relative,
}

public enum ScheduleType
{
    Immediate,
    Once,
    Interval,
}

public enum ExecutionState
{
    Idle,
    Preparing,
    Waiting,
    Running,
    Paused,
    Completed,
    Stopped,
    Error,
}

public enum FailurePolicy
{
    /// <summary>다음 동작으로 계속 진행.</summary>
    Continue,
    /// <summary>매크로를 Error 상태로 중단.</summary>
    Abort,
    /// <summary>매크로를 Completed 상태로 정상 종료.</summary>
    Stop,
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}
