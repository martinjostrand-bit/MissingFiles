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

        var viewModel = new MainViewModel(new WpfDialogService(), new JsonSettingsStore(JsonSettingsStore.DefaultPath), new ShellService());
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
