using System.Windows.Controls;
using ClickFlow.App.ViewModels;

namespace ClickFlow.App.Views;

public partial class ExecutionView : UserControl
{
    private ExecutionViewModel? _subscribed;

    public ExecutionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Resubscribe();
        Unloaded += (_, _) => Unsubscribe();
        Loaded += (_, _) => Resubscribe();
    }

    private void Resubscribe()
    {
        Unsubscribe();
        if (DataContext is not ExecutionViewModel vm) return;
        _subscribed = vm;
        vm.LogAppended += OnLogAppended;
    }

    private void Unsubscribe()
    {
        if (_subscribed is null) return;
        _subscribed.LogAppended -= OnLogAppended;
        _subscribed = null;
    }

    private void OnLogAppended(object? sender, EventArgs e)
    {
        if (LogList.Items.Count == 0) return;
        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
