using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Core.Serialization;

namespace ClickFlow.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
    }

    public string FilePath => _filePath;

    public static AppSettings CreateDefaults() => new()
    {
        MacroDirectory = AppPaths.MacrosDirectory,
        ScreenshotDirectory = AppPaths.ScreenshotsDirectory,
    };

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            return CreateDefaults();
        }

        AppSettings settings;
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
            settings = MacroJson.Deserialize<AppSettings>(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CreateDefaults();
        }

        if (string.IsNullOrWhiteSpace(settings.MacroDirectory))
        {
            settings.MacroDirectory = AppPaths.MacrosDirectory;
        }
        if (string.IsNullOrWhiteSpace(settings.ScreenshotDirectory))
        {
            settings.ScreenshotDirectory = AppPaths.ScreenshotsDirectory;
        }
        return settings;
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        return AtomicFile.WriteAllTextAsync(_filePath, MacroJson.Serialize(settings), ct);
    }
}
