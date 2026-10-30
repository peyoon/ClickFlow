using ClickFlow.Core.Models;
using ClickFlow.Storage;
using Xunit;

namespace ClickFlow.Tests;

public class StorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "clickflow-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task Repository_SaveLoadDelete_RoundTrip()
    {
        var repo = new JsonMacroRepository(_dir);
        Assert.True(Directory.Exists(_dir));
        Assert.Equal(Path.GetFullPath(_dir), repo.Directory);

        var macro = new Macro { Name = "수강신청", Profile = "학교", Actions = { new MouseClickAction { X = 1, Y = 2 } } };
        await repo.SaveAsync(macro);

        var loaded = await repo.LoadAsync(macro.Id);
        Assert.NotNull(loaded);
        Assert.Equal("수강신청", loaded.Name);
        Assert.Single(loaded.Actions);

        var all = await repo.LoadAllAsync();
        Assert.Single(all);
        Assert.Empty(repo.LastLoadErrors);

        await repo.DeleteAsync(macro.Id);
        Assert.Null(await repo.LoadAsync(macro.Id));
        Assert.Empty(Directory.GetFiles(_dir, "*.json"));
    }

    [Fact]
    public async Task Repository_Rename_LeavesSingleFile()
    {
        var repo = new JsonMacroRepository(_dir);
        var macro = new Macro { Name = "first" };
        await repo.SaveAsync(macro);
        macro.Name = "second: name/with*bad|chars";
        await repo.SaveAsync(macro);

        var files = Directory.GetFiles(_dir, "*.json");
        Assert.Single(files);
        Assert.StartsWith("second", Path.GetFileName(files[0]));
        Assert.EndsWith("." + macro.Id.ToString("N")[..8] + ".json", files[0]);
        Assert.Equal("second: name/with*bad|chars", (await repo.LoadAsync(macro.Id))?.Name);
    }

    [Fact]
    public async Task Repository_CorruptFile_ReportedButOthersLoad()
    {
        var repo = new JsonMacroRepository(_dir);
        await repo.SaveAsync(new Macro { Name = "b", Profile = "z" });
        await repo.SaveAsync(new Macro { Name = "a", Profile = "z" });
        await repo.SaveAsync(new Macro { Name = "c", Profile = "a" });
        await File.WriteAllTextAsync(Path.Combine(_dir, "broken.deadbeef.json"), "{ not json");

        var all = await repo.LoadAllAsync();

        Assert.Equal(new[] { "c", "a", "b" }, all.Select(m => m.Name));
        var error = Assert.Single(repo.LastLoadErrors);
        Assert.Contains("broken.deadbeef.json", error);
    }

    [Fact]
    public async Task Repository_ImportExport_AssignsNewIdOnCollision()
    {
        var repo = new JsonMacroRepository(_dir);
        var macro = new Macro { Name = "orig" };
        await repo.SaveAsync(macro);
        var exportPath = Path.Combine(_dir, "export", "m.json");
        await repo.ExportAsync(macro, exportPath);
        Assert.True(File.Exists(exportPath));

        var imported = await repo.ImportAsync(exportPath);

        Assert.NotEqual(macro.Id, imported.Id);
        Assert.Equal(2, (await repo.LoadAllAsync()).Count);
    }

    [Fact]
    public async Task SettingsStore_MissingFile_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(Path.Combine(_dir, "settings.json"));

        var settings = await store.LoadAsync();

        Assert.Equal(AppPaths.MacrosDirectory, settings.MacroDirectory);
        Assert.Equal(AppPaths.ScreenshotsDirectory, settings.ScreenshotDirectory);
        Assert.Equal("F8", settings.StartHotkey);
    }

    [Fact]
    public async Task SettingsStore_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        await File.WriteAllTextAsync(path, "garbage");
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync();

        Assert.Equal(AppPaths.MacrosDirectory, settings.MacroDirectory);
    }

    [Fact]
    public async Task SettingsStore_SaveLoad_RoundTrip()
    {
        var store = new JsonSettingsStore(Path.Combine(_dir, "nested", "settings.json"));
        var settings = new AppSettings
        {
            MacroDirectory = "C:/m",
            ScreenshotDirectory = "C:/s",
            StartHotkey = "F5",
            DarkMode = false,
            HighResolutionWindowMs = 42,
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal("C:/m", loaded.MacroDirectory);
        Assert.Equal("C:/s", loaded.ScreenshotDirectory);
        Assert.Equal("F5", loaded.StartHotkey);
        Assert.False(loaded.DarkMode);
        Assert.Equal(42, loaded.HighResolutionWindowMs);
    }

    [Fact]
    public void AppPaths_AreUnderLocalAppData()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickFlow");
        Assert.Equal(root, AppPaths.RootDirectory);
        Assert.StartsWith(root, AppPaths.MacrosDirectory);
        Assert.StartsWith(root, AppPaths.ScreenshotsDirectory);
        Assert.StartsWith(root, AppPaths.LogsDirectory);
        Assert.StartsWith(root, AppPaths.SettingsFile);
    }
}
