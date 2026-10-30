using ClickFlow.App.Views;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;

namespace ClickFlow.App.Services;

public interface IPickerService
{
    Task<ScreenPoint?> PickPointAsync();
    Task<ScreenRegion?> PickRegionAsync();
}

public sealed class PickerService : IPickerService
{
    private readonly IWindowService _windows;
    private readonly IInputDriver _input;

    public PickerService(IWindowService windows, IInputDriver input)
    {
        _windows = windows;
        _input = input;
    }

    public Task<ScreenPoint?> PickPointAsync()
    {
        var overlay = new PickerOverlayWindow(_windows.GetVirtualScreenBounds(), _input, PickerMode.Point);
        overlay.ShowDialog();
        return Task.FromResult(overlay.PickedPoint);
    }

    public Task<ScreenRegion?> PickRegionAsync()
    {
        var overlay = new PickerOverlayWindow(_windows.GetVirtualScreenBounds(), _input, PickerMode.Region);
        overlay.ShowDialog();
        return Task.FromResult(overlay.PickedRegion);
    }
}
