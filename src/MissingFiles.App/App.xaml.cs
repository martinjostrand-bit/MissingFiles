using System.Windows;
using System.Windows.Threading;

using MissingFiles.App.Services;
using MissingFiles.App.ViewModels;
using MissingFiles.App.Views;

namespace MissingFiles.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        // Checked first: with an invalid data folder, no settings are read or written (REQ-04a).
        var value = Environment.GetEnvironmentVariable(AppDataFolder.VariableName);
        if (!AppDataFolder.TryResolve(value, out var dataFolder))
        {
            MessageBox.Show(
                $"MissingFiles cannot start, because the data folder is not a full path to a folder: {value}\n\nUse a full path, such as C:\\MissingFilesData, and start again.",
                "MissingFiles",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var settings = new JsonSettingsStore(AppDataFolder.SettingsPath(dataFolder));
        var viewModel = new MainViewModel(new WpfDialogService(), settings, new ShellService(), userFileTypesPath: AppDataFolder.UserFileTypesPath(dataFolder));
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        viewModel.Initialize();
        window.Show();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the application running; the user's files are never changed by the UI itself.
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "MissingFiles",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
