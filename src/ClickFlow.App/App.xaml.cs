using System.Windows;
using System.Windows.Threading;
using ClickFlow.App.Services;
using ClickFlow.App.ViewModels;
using ClickFlow.App.Views;
using ClickFlow.Core.Interfaces;
using ClickFlow.Engine;
using ClickFlow.Input;
using ClickFlow.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClickFlow.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => _services ?? throw new InvalidOperationException("서비스 컨테이너가 아직 초기화되지 않았습니다.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        WindowsInputDriver.EnsureTimerResolution();

        var settingsStore = new JsonSettingsStore(AppPaths.SettingsFile);
        var settings = await settingsStore.LoadAsync();
        var macroDirectory = string.IsNullOrWhiteSpace(settings.MacroDirectory) ? AppPaths.EnsureMacrosDirectory() : AppPaths.EnsureDirectory(settings.MacroDirectory);

        var services = new ServiceCollection();

        services.AddSingleton<ISettingsStore>(settingsStore);
        services.AddSingleton<IMacroRepository>(new JsonMacroRepository(macroDirectory));
        services.AddSingleton<MacroLibrary>();

        services.AddSingleton<IInputDriver, WindowsInputDriver>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IScreenService, ScreenService>();
        services.AddSingleton<IInputRecorder, LowLevelInputRecorder>();

        services.AddSingleton<IScheduler>(sp => new HighResolutionScheduler(sp.GetRequiredService<MacroLibrary>().Settings.HighResolutionWindowMs));
        services.AddSingleton<IConditionEvaluator, ConditionEngine>();
        services.AddSingleton(sp => new ActionExecutor(
            sp.GetRequiredService<IInputDriver>(),
            sp.GetRequiredService<IScreenService>(),
            sp.GetRequiredService<IScheduler>(),
            sp.GetRequiredService<IConditionEvaluator>(),
            () =>
            {
                var dir = sp.GetRequiredService<MacroLibrary>().Settings.ScreenshotDirectory;
                return string.IsNullOrWhiteSpace(dir) ? AppPaths.EnsureScreenshotsDirectory() : AppPaths.EnsureDirectory(dir);
            }));
        services.AddSingleton<IMacroExecutor, MacroExecutor>();
        services.AddSingleton<ExecutionService>();

        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<HotkeyCoordinator>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPickerService, PickerService>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MacroListViewModel>();
        services.AddSingleton<MacroEditorViewModel>();
        services.AddSingleton<RecorderViewModel>();
        services.AddSingleton<ExecutionViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<MainViewModel>());
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
        await _services.GetRequiredService<MainViewModel>().InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var execution = _services?.GetService<ExecutionService>();
        if (execution is { IsBusy: true }) execution.Stop();
        MessageBox.Show(e.Exception.Message, "ClickFlow 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
