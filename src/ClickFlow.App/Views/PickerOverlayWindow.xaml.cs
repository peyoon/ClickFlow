using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.App.Views;

public enum PickerMode
{
    Point,
    Region,
}

public partial class PickerOverlayWindow : Window
{
    private readonly ScreenRegion _bounds;
    private readonly IInputDriver _input;
    private readonly PickerMode _mode;
    private readonly DispatcherTimer _poll;
    private Point? _dragStart;
    private double _scaleX = 1;
    private double _scaleY = 1;

    public PickerOverlayWindow(ScreenRegion bounds, IInputDriver input, PickerMode mode)
    {
        InitializeComponent();
        _bounds = bounds;
        _input = input;
        _mode = mode;

        Left = bounds.X;
        Top = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;

        HudTitle.Text = mode == PickerMode.Point ? "좌표 지정 모드" : "영역 지정 모드";
        HudHint.Text = mode == PickerMode.Point
            ? "클릭 또는 Space로 선택 · Esc 취소"
            : "드래그하여 영역 선택 · Esc 취소";

        _poll = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _poll.Tick += (_, _) => UpdateFromCursor();

        Loaded += OnLoaded;
        Closed += (_, _) => _poll.Stop();
        MouseLeftButtonDown += OnMouseDown;
        MouseLeftButtonUp += OnMouseUp;
        MouseMove += (_, _) => UpdateFromCursor();
        KeyDown += OnKeyDown;
    }

    public ScreenPoint? PickedPoint { get; private set; }
    public ScreenRegion? PickedRegion { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is { } target)
        {
            _scaleX = target.TransformToDevice.M11;
            _scaleY = target.TransformToDevice.M22;
        }
        Dim.Width = ActualWidth;
        Dim.Height = ActualHeight;
        Activate();
        Focus();
        _poll.Start();
        UpdateFromCursor();
    }

    private void UpdateFromCursor()
    {
        var cursor = _input.GetCursorPosition();
        var local = ToLocal(cursor);

        CrossH.X1 = 0;
        CrossH.X2 = ActualWidth;
        CrossH.Y1 = CrossH.Y2 = local.Y;
        CrossV.Y1 = 0;
        CrossV.Y2 = ActualHeight;
        CrossV.X1 = CrossV.X2 = local.X;

        if (_dragStart is { } start)
        {
            var rect = new Rect(start, local);
            System.Windows.Controls.Canvas.SetLeft(Selection, rect.X);
            System.Windows.Controls.Canvas.SetTop(Selection, rect.Y);
            Selection.Width = rect.Width;
            Selection.Height = rect.Height;
            Selection.Visibility = Visibility.Visible;
            var region = ToRegion(rect);
            HudValue.Text = $"({region.X}, {region.Y})  {region.Width} × {region.Height}";
        }
        else
        {
            HudValue.Text = $"X: {cursor.X}   Y: {cursor.Y}";
        }

        var hudX = local.X + 20;
        var hudY = local.Y + 20;
        if (hudX + Hud.ActualWidth > ActualWidth) hudX = local.X - Hud.ActualWidth - 20;
        if (hudY + Hud.ActualHeight > ActualHeight) hudY = local.Y - Hud.ActualHeight - 20;
        System.Windows.Controls.Canvas.SetLeft(Hud, hudX);
        System.Windows.Controls.Canvas.SetTop(Hud, hudY);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode == PickerMode.Point)
        {
            Commit(_input.GetCursorPosition());
            return;
        }
        _dragStart = ToLocal(_input.GetCursorPosition());
        CaptureMouse();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode != PickerMode.Region || _dragStart is not { } start) return;
        ReleaseMouseCapture();
        var end = ToLocal(_input.GetCursorPosition());
        var rect = new Rect(start, end);
        _dragStart = null;
        if (rect.Width < 2 || rect.Height < 2) return;
        PickedRegion = ToRegion(rect);
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DialogResult = false;
                Close();
                break;
            case Key.Space or Key.Enter when _mode == PickerMode.Point:
                Commit(_input.GetCursorPosition());
                break;
        }
    }

    private void Commit(ScreenPoint point)
    {
        PickedPoint = point;
        DialogResult = true;
        Close();
    }

    private Point ToLocal(ScreenPoint p) => new((p.X - _bounds.X) / _scaleX, (p.Y - _bounds.Y) / _scaleY);

    private ScreenRegion ToRegion(Rect local) => new()
    {
        X = (int)Math.Round(local.X * _scaleX) + _bounds.X,
        Y = (int)Math.Round(local.Y * _scaleY) + _bounds.Y,
        Width = (int)Math.Round(local.Width * _scaleX),
        Height = (int)Math.Round(local.Height * _scaleY),
    };
}
