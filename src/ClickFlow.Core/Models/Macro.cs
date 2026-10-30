using System.Text.Json.Serialization;

namespace ClickFlow.Core.Models;

public sealed class Schedule
{
    public ScheduleType Type { get; set; } = ScheduleType.Immediate;
    public DateTime? StartTime { get; set; }
    public int IntervalMs { get; set; } = 60_000;
    public int RepeatCount { get; set; } = 1;

    [JsonIgnore]
    public string Summary => Type switch
    {
        ScheduleType.Once => StartTime.HasValue ? StartTime.Value.ToString("yyyy-MM-dd HH:mm:ss.fff") : "시각 미지정",
        ScheduleType.Interval => $"{IntervalMs} ms 간격 · {(RepeatCount <= 0 ? "무한" : RepeatCount + "회")}",
        _ => "즉시 실행",
    };
}

public sealed class TargetWindow
{
    public string? TitleContains { get; set; }
    public string? ProcessName { get; set; }
    public bool BringToFront { get; set; } = true;

    [JsonIgnore]
    public bool IsSpecified => !string.IsNullOrWhiteSpace(TitleContains) || !string.IsNullOrWhiteSpace(ProcessName);

    public override string ToString() =>
        IsSpecified ? $"{TitleContains ?? ""}{(ProcessName is null ? "" : $" ({ProcessName})")}".Trim() : "지정 안 함";
}

public sealed class MacroSettings
{
    public int MaxRetries { get; set; }
    public int RetryIntervalMs { get; set; } = 500;
    public bool StopOnError { get; set; } = true;
    public bool RestoreCursorAfterRun { get; set; }
    public int DefaultDelayBetweenActionsMs { get; set; }
}

public sealed class Macro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "새 매크로";
    public string? Description { get; set; }
    public string Profile { get; set; } = "기본";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public Schedule Schedule { get; set; } = new();
    public TargetWindow TargetWindow { get; set; } = new();
    public MacroSettings Settings { get; set; } = new();
    public List<ActionStep> Actions { get; set; } = new();

    public Macro Clone()
    {
        return new Macro
        {
            Id = Guid.NewGuid(),
            Name = Name + " (복사본)",
            Description = Description,
            Profile = Profile,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Schedule = new Schedule
            {
                Type = Schedule.Type,
                StartTime = Schedule.StartTime,
                IntervalMs = Schedule.IntervalMs,
                RepeatCount = Schedule.RepeatCount,
            },
            TargetWindow = new TargetWindow
            {
                TitleContains = TargetWindow.TitleContains,
                ProcessName = TargetWindow.ProcessName,
                BringToFront = TargetWindow.BringToFront,
            },
            Settings = new MacroSettings
            {
                MaxRetries = Settings.MaxRetries,
                RetryIntervalMs = Settings.RetryIntervalMs,
                StopOnError = Settings.StopOnError,
                RestoreCursorAfterRun = Settings.RestoreCursorAfterRun,
                DefaultDelayBetweenActionsMs = Settings.DefaultDelayBetweenActionsMs,
            },
            Actions = Actions.Select(a => a.Clone()).ToList(),
        };
    }
}
