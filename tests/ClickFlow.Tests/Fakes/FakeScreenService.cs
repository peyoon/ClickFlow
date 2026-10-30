using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.Tests.Fakes;

public sealed class FakeScreenService : IScreenService
{
    public Dictionary<(int X, int Y), RgbColor> Pixels { get; } = new();

    public RgbColor DefaultPixel { get; set; } = new() { R = 0, G = 0, B = 0 };

    public Dictionary<string, ScreenPoint> ImageHits { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int PixelQueries { get; private set; }

    public int ImageQueries { get; private set; }

    public List<string> SavedScreenshots { get; } = new();

    public RgbColor GetPixelColor(int x, int y)
    {
        PixelQueries++;
        return Pixels.TryGetValue((x, y), out var color) ? color : DefaultPixel;
    }

    public byte[] CapturePng(ScreenRegion? region) => Array.Empty<byte>();

    public string SaveScreenshot(string directory, string? fileName, ScreenRegion? region)
    {
        var path = Path.Combine(directory, fileName ?? "shot.png");
        SavedScreenshots.Add(path);
        return path;
    }

    public ScreenPoint? FindImage(string imagePath, ScreenRegion? searchRegion, double similarity)
    {
        ImageQueries++;
        return ImageHits.TryGetValue(imagePath, out var point) ? point : null;
    }
}
