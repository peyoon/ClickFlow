using ClickFlow.Core.Interfaces;
using ClickFlow.Core.Models;
using ClickFlow.Core.Serialization;

namespace ClickFlow.Storage;

public sealed class JsonMacroRepository : IMacroRepository
{
    private const string Extension = ".json";
    private const int MaxNameLength = 60;

    private readonly string _directory;
    private IReadOnlyList<string> _lastLoadErrors = Array.Empty<string>();

    public JsonMacroRepository(string directory)
    {
        _directory = Path.GetFullPath(directory);
        System.IO.Directory.CreateDirectory(_directory);
    }

    public string Directory => _directory;

    public IReadOnlyList<string> LastLoadErrors => _lastLoadErrors;

    public async Task<IReadOnlyList<Macro>> LoadAllAsync(CancellationToken ct = default)
    {
        var macros = new List<Macro>();
        var errors = new List<string>();

        foreach (var file in System.IO.Directory.EnumerateFiles(_directory, "*" + Extension))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                macros.Add(await ReadMacroAsync(file, ct).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        _lastLoadErrors = errors;
        return macros
            .OrderBy(m => m.Profile, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Macro?> LoadAsync(Guid id, CancellationToken ct = default)
    {
        foreach (var file in FindFilesById(id))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var macro = await ReadMacroAsync(file, ct).ConfigureAwait(false);
                if (macro.Id == id)
                {
                    return macro;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _lastLoadErrors = new[] { $"{Path.GetFileName(file)}: {ex.Message}" };
            }
        }
        return null;
    }

    public async Task SaveAsync(Macro macro, CancellationToken ct = default)
    {
        macro.UpdatedAt = DateTime.Now;
        var targetPath = BuildPath(macro);
        await AtomicFile.WriteAllTextAsync(targetPath, MacroJson.Serialize(macro), ct).ConfigureAwait(false);

        foreach (var stale in FindFilesById(macro.Id))
        {
            if (!PathsEqual(stale, targetPath))
            {
                File.Delete(stale);
            }
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        foreach (var file in FindFilesById(id))
        {
            ct.ThrowIfCancellationRequested();
            File.Delete(file);
        }
        return Task.CompletedTask;
    }

    public async Task<Macro> ImportAsync(string filePath, CancellationToken ct = default)
    {
        var macro = await ReadMacroAsync(filePath, ct).ConfigureAwait(false);
        if (await LoadAsync(macro.Id, ct).ConfigureAwait(false) is not null)
        {
            macro.Id = Guid.NewGuid();
        }
        macro.CreatedAt = DateTime.Now;
        await SaveAsync(macro, ct).ConfigureAwait(false);
        return macro;
    }

    public Task ExportAsync(Macro macro, string filePath, CancellationToken ct = default)
    {
        return AtomicFile.WriteAllTextAsync(Path.GetFullPath(filePath), MacroJson.Serialize(macro), ct);
    }

    private static async Task<Macro> ReadMacroAsync(string file, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        return MacroJson.Deserialize<Macro>(json);
    }

    private IEnumerable<string> FindFilesById(Guid id)
    {
        return System.IO.Directory.EnumerateFiles(_directory, "*." + ShortId(id) + Extension);
    }

    private string BuildPath(Macro macro)
    {
        return Path.Combine(_directory, $"{SanitizeName(macro.Name)}.{ShortId(macro.Id)}{Extension}");
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) || c == '.' ? '_' : c).ToArray();
        var sanitized = new string(chars).Trim('_', ' ');
        if (sanitized.Length == 0)
        {
            sanitized = "macro";
        }
        return sanitized.Length > MaxNameLength ? sanitized[..MaxNameLength] : sanitized;
    }

    private static bool PathsEqual(string a, string b)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
    }
}
