using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ClickFlow.App.ViewModels;

namespace ClickFlow.App.Views;

public partial class NestedActionList : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(NestedActionList), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<ActionItemViewModel>), typeof(NestedActionList), new PropertyMetadata(null));

    public static readonly DependencyProperty AddSlotProperty =
        DependencyProperty.Register(nameof(AddSlot), typeof(string), typeof(NestedActionList), new PropertyMetadata("then"));

    public NestedActionList()
    {
        InitializeComponent();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public ObservableCollection<ActionItemViewModel>? Items
    {
        get => (ObservableCollection<ActionItemViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string AddSlot
    {
        get => (string)GetValue(AddSlotProperty);
        set => SetValue(AddSlotProperty, value);
    }
}
