namespace ClickFlow.Storage;

public static class AppPaths
{
    public static string RootDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickFlow");

    public static string MacrosDirectory { get; } = Path.Combine(RootDirectory, "Macros");

    public static string ScreenshotsDirectory { get; } = Path.Combine(RootDirectory, "Screenshots");

    public static string LogsDirectory { get; } = Path.Combine(RootDirectory, "Logs");

    public static string SettingsFile { get; } = Path.Combine(RootDirectory, "settings.json");

    public static string EnsureRootDirectory() => EnsureDirectory(RootDirectory);

    public static string EnsureMacrosDirectory() => EnsureDirectory(MacrosDirectory);

    public static string EnsureScreenshotsDirectory() => EnsureDirectory(ScreenshotsDirectory);

    public static string EnsureLogsDirectory() => EnsureDirectory(LogsDirectory);

    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
