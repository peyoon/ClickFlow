using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ClickFlow.App.Views;

public sealed class ScreenPreview : Control
{
    public static readonly DependencyProperty PointProperty =
        DependencyProperty.Register(nameof(Point), typeof(ScreenPoint), typeof(ScreenPreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ScreenPreview)d).OnPointChanged()));

    private BitmapSource? _capture;
    private ScreenRegion? _captureBounds;
    private DateTime _capturedAt;

    static ScreenPreview()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ScreenPreview), new FrameworkPropertyMetadata(typeof(ScreenPreview)));
    }

    public ScreenPreview()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        MouseLeftButtonDown += (_, _) => Refresh();
        ToolTip = "클릭하면 화면을 다시 캡처합니다";
    }

    public ScreenPoint? Point
    {
        get => (ScreenPoint?)GetValue(PointProperty);
        set => SetValue(PointProperty, value);
    }

    private void OnPointChanged()
    {
        if (_capture is null || DateTime.Now - _capturedAt > TimeSpan.FromSeconds(30)) Refresh();
    }

    private void Refresh()
    {
        if (DesignerProperties.GetIsInDesignMode(this)) return;
        try
        {
            var services = App.Current.Services;
            var screen = services.GetRequiredService<IScreenService>();
            var windows = services.GetRequiredService<IWindowService>();
            _captureBounds = windows.GetVirtualScreenBounds();
            var png = screen.CapturePng(_captureBounds);
            using var stream = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            _capture = image;
            _capturedAt = DateTime.Now;
        }
        catch (Exception)
        {
            _capture = null;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRoundedRectangle((Brush)FindResource("Brush.Well"), null, bounds, 6, 6);

        if (_capture is null || _captureBounds is null)
        {
            var text = new FormattedText("미리보기를 불러오려면 클릭", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, (Brush)FindResource("Brush.TextMuted"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new System.Windows.Point((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2));
            return;
        }

        var scale = Math.Min(ActualWidth / _capture.PixelWidth, ActualHeight / _capture.PixelHeight);
        var w = _capture.PixelWidth * scale;
        var h = _capture.PixelHeight * scale;
        var offset = new System.Windows.Point((ActualWidth - w) / 2, (ActualHeight - h) / 2);
        dc.DrawImage(_capture, new Rect(offset, new Size(w, h)));

        if (Point is not { } p) return;
        var x = offset.X + (p.X - _captureBounds.X) * scale;
        var y = offset.Y + (p.Y - _captureBounds.Y) * scale;
        var accent = (Brush)FindResource("Brush.Accent");
        var pen = new Pen(accent, 1.5);
        dc.DrawEllipse(null, pen, new System.Windows.Point(x, y), 9, 9);
        dc.DrawEllipse(accent, null, new System.Windows.Point(x, y), 2.5, 2.5);
        dc.DrawLine(pen, new System.Windows.Point(x - 14, y), new System.Windows.Point(x - 10, y));
        dc.DrawLine(pen, new System.Windows.Point(x + 10, y), new System.Windows.Point(x + 14, y));
        dc.DrawLine(pen, new System.Windows.Point(x, y - 14), new System.Windows.Point(x, y - 10));
        dc.DrawLine(pen, new System.Windows.Point(x, y + 10), new System.Windows.Point(x, y + 14));
    }
}
