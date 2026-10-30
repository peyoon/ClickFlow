using ClickFlow.Core.Models;
using ClickFlow.Core.Serialization;
using Xunit;

namespace ClickFlow.Tests;

public class SerializationTests
{
    [Fact]
    public void RoundTrip_AllActionTypes_ProducesIdenticalJson()
    {
        var macro = new Macro
        {
            Name = "모든 동작",
            Schedule = new Schedule { Type = ScheduleType.Once, StartTime = new DateTime(2026, 11, 2, 9, 0, 0) },
            TargetWindow = new TargetWindow { TitleContains = "Chrome" },
            Actions =
            {
                new MouseClickAction { X = 10, Y = 20, Button = MouseButton.Right, ClickCount = 2 },
                new MouseMoveAction { X = 30, Y = 40, DurationMs = 100, CoordinateMode = CoordinateMode.Window },
                new MouseDownAction { X = 1, Y = 2 },
                new MouseUpAction { X = 1, Y = 2 },
                new MouseScrollAction { Delta = 240 },
                new KeyPressAction { Key = "ENTER", Ctrl = true },
                new KeyDownAction { Key = "SHIFT" },
                new KeyUpAction { Key = "SHIFT" },
                new TypeTextAction { Text = "hello", IntervalMs = 5 },
                new WaitAction { Milliseconds = 250 },
                new ScreenshotAction { FileName = "a.png", Region = new ScreenRegion { X = 0, Y = 0, Width = 10, Height = 10 } },
                new ConditionAction
                {
                    Condition = new PixelColorCondition { X = 5, Y = 5, Color = new RgbColor { R = 1, G = 2, B = 3 }, Tolerance = 4 },
                    ThenActions = { new WaitAction { Milliseconds = 1 } },
                    ElseActions = { new KeyPressAction { Key = "ESCAPE" } },
                    RetryCount = 2,
                    OnFailure = FailurePolicy.Abort,
                },
                new ConditionAction { Condition = new ImageExistsCondition { ImagePath = "img.png", ClickOnFound = true } },
                new ConditionAction { Condition = new WindowExistsCondition { TitleContains = "메모장" } },
                new ConditionAction { Condition = new WindowTitleCondition { TitleContains = "x", Negate = true } },
                new ConditionAction { Condition = new TimeoutCondition { TimeoutMs = 10 } },
                new RepeatAction { Count = 3, Actions = { new MouseClickAction { X = 7, Y = 8 }, new WaitAction() } },
                new StopAction { Success = false },
            },
        };

        var json = MacroJson.Serialize(macro);
        var restored = MacroJson.Deserialize<Macro>(json);
        var json2 = MacroJson.Serialize(restored);

        Assert.Equal(json, json2);
        Assert.Contains("\"type\": \"Click\"", json);
        Assert.Equal(macro.Actions.Count, restored.Actions.Count);
        Assert.IsType<MouseClickAction>(restored.Actions[0]);
        var condition = Assert.IsType<ConditionAction>(restored.Actions[11]);
        Assert.IsType<PixelColorCondition>(condition.Condition);
        Assert.Single(condition.ThenActions);
        Assert.Single(condition.ElseActions);
        var repeat = Assert.IsType<RepeatAction>(restored.Actions[16]);
        Assert.Equal(2, repeat.Actions.Count);
    }

    [Fact]
    public void SampleJson_FromSpec_Deserializes()
    {
        const string json = """
            { "name": "수강신청", "schedule": { "type": "Once", "startTime": "2026-11-02T09:00:00.000" }, "actions": [ { "type": "Click", "x": 1200, "y": 700 }, { "type": "Wait", "milliseconds": 300 }, { "type": "Click", "x": 1100, "y": 650 }, { "type": "Key", "key": "ENTER" } ] }
            """;

        var macro = MacroJson.Deserialize<Macro>(json);

        Assert.Equal("수강신청", macro.Name);
        Assert.Equal(ScheduleType.Once, macro.Schedule.Type);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), macro.Schedule.StartTime);
        Assert.Equal(4, macro.Actions.Count);
        var click = Assert.IsType<MouseClickAction>(macro.Actions[0]);
        Assert.Equal(1200, click.X);
        Assert.Equal(700, click.Y);
        Assert.Equal(300, Assert.IsType<WaitAction>(macro.Actions[1]).Milliseconds);
        Assert.Equal(1100, Assert.IsType<MouseClickAction>(macro.Actions[2]).X);
        Assert.Equal("ENTER", Assert.IsType<KeyPressAction>(macro.Actions[3]).Key);
    }
}
