using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickFlow.App.ViewModels;
using ClickFlow.Core.Models;

namespace ClickFlow.App.Views;

public partial class MacroEditorView : UserControl
{
    private Point _dragOrigin;
    private ActionItemViewModel? _dragItem;

    public MacroEditorView()
    {
        InitializeComponent();
    }

    private MacroEditorViewModel? ViewModel => DataContext as MacroEditorViewModel;

    private void WindowPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: WindowInfo window } box) return;
        ViewModel?.UseWindowCommand.Execute(window);
        box.SelectedItem = null;
    }

    private void ActionList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragOrigin = e.GetPosition(null);
        _dragItem = ItemUnder(e.OriginalSource as DependencyObject);
    }

    private void ActionList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(null) - _dragOrigin;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(ActionList, item, DragDropEffects.Move);
    }

    private void ActionList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ActionItemViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void ActionList_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || e.Data.GetData(typeof(ActionItemViewModel)) is not ActionItemViewModel dragged) return;
        var target = ItemUnder(e.OriginalSource as DependencyObject);
        var newIndex = target is null ? ViewModel.Actions.Count - 1 : ViewModel.Actions.IndexOf(target);
        ViewModel.MoveTo(dragged, newIndex);
    }

    private static ActionItemViewModel? ItemUnder(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem)
        {
            source = VisualTreeHelper.GetParent(source);
        }
        return (source as ListBoxItem)?.DataContext as ActionItemViewModel;
    }
}
