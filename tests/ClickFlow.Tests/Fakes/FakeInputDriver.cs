using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Tests.Fakes;

public sealed class FakeInputDriver : IInputDriver
{
    private int _callCount;

    public List<string> Log { get; } = new();

    public ScreenPoint Cursor { get; set; } = new(0, 0);

    public int ThrowOnCall { get; set; }

    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("fake input failure");

    public ScreenPoint GetCursorPosition() => Cursor;

    public void MoveTo(int x, int y)
    {
        Record($"MoveTo({x},{y})");
        Cursor = new ScreenPoint(x, y);
    }

    public void MouseDown(MouseButton button) => Record($"MouseDown({button})");

    public void MouseUp(MouseButton button) => Record($"MouseUp({button})");

    public void Click(int x, int y, MouseButton button, int clickCount, bool moveFirst)
    {
        Record($"Click({x},{y},{button},{clickCount})");
        if (moveFirst)
        {
            Cursor = new ScreenPoint(x, y);
        }
    }

    public void Scroll(int delta) => Record($"Scroll({delta})");

    public void KeyDown(string key, KeyModifiers modifiers) => Record($"KeyDown({key},{modifiers})");

    public void KeyUp(string key, KeyModifiers modifiers) => Record($"KeyUp({key},{modifiers})");

    public void KeyPress(string key, KeyModifiers modifiers) => Record($"KeyPress({key},{modifiers})");

    public void TypeText(string text, int intervalMs, CancellationToken ct) => Record($"TypeText({text})");

    private void Record(string entry)
    {
        var call = Interlocked.Increment(ref _callCount);
        lock (Log)
        {
            Log.Add(entry);
        }
        if (ThrowOnCall > 0 && call == ThrowOnCall)
        {
            throw ExceptionToThrow;
        }
    }
}
