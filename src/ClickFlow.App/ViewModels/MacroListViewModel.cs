using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ClickFlow.App.Services;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickFlow.App.ViewModels;

public sealed partial class MacroListViewModel : PageViewModel
{
    private readonly MacroLibrary _library;
    private readonly ExecutionService _execution;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public MacroListViewModel(MacroLibrary library, ExecutionService execution, INavigationService navigation, IDialogService dialogs)
    {
        _library = library;
        _execution = execution;
        _navigation = navigation;
        _dialogs = dialogs;

        View = CollectionViewSource.GetDefaultView(_library.Macros);
        View.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Macro.Profile)));
        View.SortDescriptions.Add(new SortDescription(nameof(Macro.Profile), ListSortDirection.Ascending));
        View.SortDescriptions.Add(new SortDescription(nameof(Macro.Name), ListSortDirection.Ascending));
        View.Filter = Filter;

        _library.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MacroLibrary.LoadError)) OnPropertyChanged(nameof(LoadError));
        };
    }

    public override string Title => "내 매크로";

    public ICollectionView View { get; }
    public ObservableCollection<Macro> Macros => _library.Macros;
    public string? LoadError => _library.LoadError;
    public string MacroDirectory => _library.MacroDirectory;

    [ObservableProperty]
    private Macro? _selected;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => View.Refresh();

    private bool Filter(object item)
    {
        if (item is not Macro macro) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return macro.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || macro.Profile.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || (macro.Description?.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        var macro = new Macro { Name = NextName("새 매크로") };
        await _library.SaveAsync(macro);
        _navigation.OpenMacro(macro);
    }

    [RelayCommand]
    private void Open(Macro? macro)
    {
        if (macro is not null) _navigation.OpenMacro(macro);
    }

    [RelayCommand]
    private async Task RunAsync(Macro? macro)
    {
        if (macro is null || _execution.IsBusy) return;
        _navigation.NavigateTo(PageKind.Execution);
        await _execution.RunAsync(macro);
    }

    [RelayCommand]
    private async Task DuplicateAsync(Macro? macro)
    {
        if (macro is null) return;
        Selected = await _library.DuplicateAsync(macro);
    }

    [RelayCommand]
    private async Task DeleteAsync(Macro? macro)
    {
        if (macro is null) return;
        if (!_dialogs.Confirm("매크로 삭제", $"'{macro.Name}' 매크로를 삭제할까요? 되돌릴 수 없습니다.")) return;
        await _library.DeleteAsync(macro);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = _dialogs.OpenFile("ClickFlow 매크로 (*.json)|*.json|모든 파일 (*.*)|*.*");
        if (path is null) return;
        try
        {
            Selected = await _library.ImportAsync(path);
        }
        catch (Exception ex)
        {
            _dialogs.Error("가져오기 실패", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportAsync(Macro? macro)
    {
        if (macro is null) return;
        var path = _dialogs.SaveFile("ClickFlow 매크로 (*.json)|*.json", $"{macro.Name}.json");
        if (path is null) return;
        try
        {
            await _library.ExportAsync(macro, path);
        }
        catch (Exception ex)
        {
            _dialogs.Error("내보내기 실패", ex.Message);
        }
    }

    [RelayCommand]
    private Task ReloadAsync() => _library.ReloadAsync();

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_library.MacroDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.Error("폴더 열기 실패", ex.Message);
        }
    }

    private string NextName(string baseName)
    {
        var names = _library.Macros.Select(m => m.Name).ToHashSet();
        if (!names.Contains(baseName)) return baseName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!names.Contains(candidate)) return candidate;
        }
    }
}
