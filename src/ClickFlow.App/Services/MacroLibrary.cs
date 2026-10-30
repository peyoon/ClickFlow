using System.Collections.ObjectModel;
using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClickFlow.App.Services;

public sealed partial class MacroLibrary : ObservableObject
{
    private readonly IMacroRepository _repository;
    private readonly ISettingsStore _settingsStore;

    public MacroLibrary(IMacroRepository repository, ISettingsStore settingsStore)
    {
        _repository = repository;
        _settingsStore = settingsStore;
    }

    public ObservableCollection<Macro> Macros { get; } = new();

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string? _loadError;

    public IReadOnlyList<string> Profiles =>
        Macros.Select(m => m.Profile).Distinct().OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToList();

    public string MacroDirectory => _repository.Directory;

    public event EventHandler? Changed;

    public async Task InitializeAsync()
    {
        Settings = await _settingsStore.LoadAsync();
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var loaded = await _repository.LoadAllAsync();
        Macros.Clear();
        foreach (var macro in loaded) Macros.Add(macro);
        LoadError = _repository is Storage.JsonMacroRepository { LastLoadErrors.Count: > 0 } repo
            ? string.Join(Environment.NewLine, repo.LastLoadErrors)
            : null;
        RaiseChanged();
    }

    public async Task SaveAsync(Macro macro)
    {
        await _repository.SaveAsync(macro);
        var index = IndexOf(macro.Id);
        if (index < 0) Macros.Add(macro);
        else if (!ReferenceEquals(Macros[index], macro)) Macros[index] = macro;
        RaiseChanged();
    }

    public async Task DeleteAsync(Macro macro)
    {
        await _repository.DeleteAsync(macro.Id);
        var index = IndexOf(macro.Id);
        if (index >= 0) Macros.RemoveAt(index);
        RaiseChanged();
    }

    public async Task<Macro> DuplicateAsync(Macro macro)
    {
        var copy = macro.Clone();
        await SaveAsync(copy);
        return copy;
    }

    public async Task<Macro> ImportAsync(string path)
    {
        var imported = await _repository.ImportAsync(path);
        var index = IndexOf(imported.Id);
        if (index < 0) Macros.Add(imported);
        else Macros[index] = imported;
        RaiseChanged();
        return imported;
    }

    public Task ExportAsync(Macro macro, string path) => _repository.ExportAsync(macro, path);

    public async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(Settings);
        RaiseChanged();
    }

    public Macro? Find(Guid id) => Macros.FirstOrDefault(m => m.Id == id);

    private int IndexOf(Guid id)
    {
        for (var i = 0; i < Macros.Count; i++)
        {
            if (Macros[i].Id == id) return i;
        }
        return -1;
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(Profiles));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
