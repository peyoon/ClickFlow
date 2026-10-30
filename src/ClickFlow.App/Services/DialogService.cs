using Microsoft.Win32;

namespace ClickFlow.App.Services;

public interface IDialogService
{
    bool Confirm(string title, string message);
    void Info(string title, string message);
    void Error(string title, string message);
    string? OpenFile(string filter, string? initialDirectory = null);
    string? SaveFile(string filter, string suggestedName, string? initialDirectory = null);
    string? PickFolder(string? initialDirectory = null);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
            == System.Windows.MessageBoxResult.Yes;

    public void Info(string title, string message) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    public void Error(string title, string message) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

    public string? OpenFile(string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog { Filter = filter, InitialDirectory = initialDirectory ?? string.Empty };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string suggestedName, string? initialDirectory = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = suggestedName,
            InitialDirectory = initialDirectory ?? string.Empty,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog { InitialDirectory = initialDirectory ?? string.Empty };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
