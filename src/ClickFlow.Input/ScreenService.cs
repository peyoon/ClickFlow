using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Input.Native;

namespace ClickFlow.Input;

[SupportedOSPlatform("windows")]
public sealed class ScreenService : IScreenService
{
    private const int ChannelTolerance = 25;

    public RgbColor GetPixelColor(int x, int y)
    {
        var hdc = User32.GetDC(0);
        if (hdc == 0) throw new InvalidOperationException("GetDC(NULL) failed.");
        try
        {
            var colorRef = Gdi32.GetPixel(hdc, x, y);
            if (colorRef == Gdi32.CLR_INVALID)
                throw new InvalidOperationException($"GetPixel({x}, {y}) returned CLR_INVALID.");

            return new RgbColor
            {
                R = (byte)(colorRef & 0xFF),
                G = (byte)((colorRef >> 8) & 0xFF),
                B = (byte)((colorRef >> 16) & 0xFF),
            };
        }
        finally
        {
            User32.ReleaseDC(0, hdc);
        }
    }

    public byte[] CapturePng(ScreenRegion? region)
    {
        using var bitmap = Capture(region ?? VirtualScreen());
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    public string SaveScreenshot(string directory, string? fileName, ScreenRegion? region)
    {
        Directory.CreateDirectory(directory);

        var name = string.IsNullOrWhiteSpace(fileName)
            ? $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"
            : fileName.Trim();
        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            name += ".png";

        var fullPath = Path.Combine(directory, name);
        File.WriteAllBytes(fullPath, CapturePng(region));
        return fullPath;
    }

    public ScreenPoint? FindImage(string imagePath, ScreenRegion? searchRegion, double similarity)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Template image not found.", imagePath);

        var region = searchRegion ?? VirtualScreen();
        if (region.Width <= 0 || region.Height <= 0) return null;

        similarity = Math.Clamp(similarity, 0.0, 1.0);

        using var needleSource = new Bitmap(imagePath);
        using var needle = ToArgb(needleSource);
        using var haystack = Capture(region);

        if (needle.Width > haystack.Width || needle.Height > haystack.Height) return null;

        var match = TemplateMatch(haystack, needle, similarity);
        if (match is null) return null;

        var (left, top) = match.Value;
        return new ScreenPoint(region.X + left + needle.Width / 2, region.Y + top + needle.Height / 2);
    }

    // Template matching, brute force over every candidate top-left in the haystack (1 px step):
    //  1. Cheap pre-filter: the needle's 4 corners + center must each match within the per-channel
    //     tolerance, otherwise the candidate is rejected without touching the rest of the pixels.
    //  2. Full scan: count pixels whose R, G and B all differ by <= ChannelTolerance. The candidate
    //     is accepted when matched / total >= similarity, and abandoned early as soon as the number
    //     of mismatches exceeds the (1 - similarity) * total budget.
    // Both bitmaps are 32bppArgb so each pixel is a single uint read (BGRA byte order, alpha ignored).
    private static unsafe (int Left, int Top)? TemplateMatch(Bitmap haystack, Bitmap needle, double similarity)
    {
        var hayRect = new Rectangle(0, 0, haystack.Width, haystack.Height);
        var needleRect = new Rectangle(0, 0, needle.Width, needle.Height);

        var hayData = haystack.LockBits(hayRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var needleData = needle.LockBits(needleRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var hayBase = (byte*)hayData.Scan0;
            var needleBase = (byte*)needleData.Scan0;
            var hayStride = hayData.Stride;
            var needleStride = needleData.Stride;

            var nw = needle.Width;
            var nh = needle.Height;
            var total = (long)nw * nh;
            var allowedMismatches = (long)Math.Floor((1.0 - similarity) * total);

            var maxLeft = haystack.Width - nw;
            var maxTop = haystack.Height - nh;

            Span<(int X, int Y)> probes = stackalloc (int, int)[]
            {
                (0, 0),
                (nw - 1, 0),
                (0, nh - 1),
                (nw - 1, nh - 1),
                (nw / 2, nh / 2),
            };

            for (var top = 0; top <= maxTop; top++)
            {
                for (var left = 0; left <= maxLeft; left++)
                {
                    var probesPass = true;
                    foreach (var (px, py) in probes)
                    {
                        var h = *(uint*)(hayBase + (top + py) * hayStride + (left + px) * 4);
                        var n = *(uint*)(needleBase + py * needleStride + px * 4);
                        if (!PixelMatches(h, n))
                        {
                            probesPass = false;
                            break;
                        }
                    }
                    if (!probesPass) continue;

                    long mismatches = 0;
                    var rejected = false;
                    for (var y = 0; y < nh && !rejected; y++)
                    {
                        var hayRow = (uint*)(hayBase + (top + y) * hayStride) + left;
                        var needleRow = (uint*)(needleBase + y * needleStride);
                        for (var x = 0; x < nw; x++)
                        {
                            if (PixelMatches(hayRow[x], needleRow[x])) continue;
                            if (++mismatches > allowedMismatches)
                            {
                                rejected = true;
                                break;
                            }
                        }
                    }

                    if (!rejected) return (left, top);
                }
            }

            return null;
        }
        finally
        {
            needle.UnlockBits(needleData);
            haystack.UnlockBits(hayData);
        }
    }

    private static bool PixelMatches(uint a, uint b)
    {
        var db = Math.Abs((int)(a & 0xFF) - (int)(b & 0xFF));
        if (db > ChannelTolerance) return false;
        var dg = Math.Abs((int)((a >> 8) & 0xFF) - (int)((b >> 8) & 0xFF));
        if (dg > ChannelTolerance) return false;
        var dr = Math.Abs((int)((a >> 16) & 0xFF) - (int)((b >> 16) & 0xFF));
        return dr <= ChannelTolerance;
    }

    private static Bitmap Capture(ScreenRegion region)
    {
        var width = Math.Max(1, region.Width);
        var height = Math.Max(1, region.Height);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(region.X, region.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static Bitmap ToArgb(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format32bppArgb)
            return (Bitmap)source.Clone();

        var converted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(converted);
            graphics.DrawImageUnscaled(source, 0, 0);
            return converted;
        }
        catch
        {
            converted.Dispose();
            throw;
        }
    }

    private static ScreenRegion VirtualScreen() => new()
    {
        X = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN),
        Y = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN),
        Width = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN),
        Height = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN),
    };
}
