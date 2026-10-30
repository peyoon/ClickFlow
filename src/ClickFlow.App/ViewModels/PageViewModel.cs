using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClickFlow.App.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    public abstract string Title { get; }

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
    public virtual void OnNavigatedFrom() { }
}

public interface INavigationService
{
    void NavigateTo(PageKind page);
    void OpenMacro(Macro macro);
}

public enum PageKind
{
    Dashboard,
    Macros,
    Editor,
    Recorder,
    Execution,
    Settings,
}
