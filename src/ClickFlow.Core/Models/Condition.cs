using System.Text.Json.Serialization;

namespace ClickFlow.Core.Models;

public sealed class ScreenRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public override string ToString() => $"({X}, {Y}) {Width}×{Height}";
}

public sealed class RgbColor
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public override string ToString() => $"RGB({R}, {G}, {B})";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PixelColorCondition), "PixelColor")]
[JsonDerivedType(typeof(ImageExistsCondition), "ImageExists")]
[JsonDerivedType(typeof(WindowExistsCondition), "WindowExists")]
[JsonDerivedType(typeof(WindowTitleCondition), "WindowTitle")]
[JsonDerivedType(typeof(TimeoutCondition), "Timeout")]
public abstract class Condition
{
    public bool Negate { get; set; }

    public abstract string Describe();

    public virtual Condition Clone() => (Condition)MemberwiseClone();
}

public sealed class PixelColorCondition : Condition
{
    public int X { get; set; }
    public int Y { get; set; }
    public RgbColor Color { get; set; } = new();
    public int Tolerance { get; set; } = 10;

    public override string Describe() => $"{(Negate ? "NOT " : "")}픽셀({X}, {Y}) = {Color} ±{Tolerance}";

    public override Condition Clone()
    {
        var copy = (PixelColorCondition)base.Clone();
        copy.Color = new RgbColor { R = Color.R, G = Color.G, B = Color.B };
        return copy;
    }
}

public sealed class ImageExistsCondition : Condition
{
    public string ImagePath { get; set; } = string.Empty;
    public ScreenRegion? SearchRegion { get; set; }
    public double Similarity { get; set; } = 0.9;
    public bool ClickOnFound { get; set; }

    public override string Describe() =>
        $"{(Negate ? "NOT " : "")}이미지 '{Path.GetFileName(ImagePath)}' 존재 (유사도 {Similarity:P0})";
}

public sealed class WindowExistsCondition : Condition
{
    public string? TitleContains { get; set; }
    public string? ProcessName { get; set; }

    public override string Describe() =>
        $"{(Negate ? "NOT " : "")}창 존재: {TitleContains ?? ProcessName ?? "?"}";
}

public sealed class WindowTitleCondition : Condition
{
    public string TitleContains { get; set; } = string.Empty;

    public override string Describe() => $"{(Negate ? "NOT " : "")}활성 창 제목에 '{TitleContains}' 포함";
}

public sealed class TimeoutCondition : Condition
{
    public int TimeoutMs { get; set; } = 5000;

    public override string Describe() => $"{(Negate ? "NOT " : "")}매크로 시작 후 {TimeoutMs} ms 경과";
}
