using System.ComponentModel;
using System.Windows;
using ClickFlow.App.ViewModels;

namespace ClickFlow.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _exitRequested;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += (_, _) => _tray?.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized || !_viewModel.Settings.MinimizeToTray) return;
        EnsureTray();
        Hide();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested || !_viewModel.Execution.IsBusy) return;
        var result = MessageBox.Show("매크로가 실행 중입니다. 중지하고 종료할까요?", "ClickFlow", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _viewModel.Execution.Stop();
    }

    private void EnsureTray()
    {
        if (_tray is not null) return;
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "ClickFlow",
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => Restore();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("열기", null, (_, _) => Restore());
        menu.Items.Add("실행 중지", null, (_, _) => _viewModel.Execution.Stop());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) =>
        {
            _exitRequested = true;
            Close();
        });
        _tray.ContextMenuStrip = menu;
    }

    private void Restore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
