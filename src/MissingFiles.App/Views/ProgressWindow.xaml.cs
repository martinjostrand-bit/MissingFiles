using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

using MissingFiles.App.ViewModels;

namespace MissingFiles.App.Views;

/// <summary>
/// Modal progress dialog. It stays open until the work has ended: closing it
/// (the X button, Alt+F4, Esc or Cancel) only requests cancellation.
/// </summary>
public partial class ProgressWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _done;

    public ProgressWindow()
    {
        InitializeComponent();

        // Refresh the elapsed time independently of progress reports (REQ-14).
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, (_, _) => ViewModel?.RefreshElapsed(), Dispatcher);
        _timer.Start();
    }

    private ProgressViewModel? ViewModel => DataContext as ProgressViewModel;

    /// <summary>Closes the dialog after the work has ended.</summary>
    public void CloseWhenDone()
    {
        _done = true;
        _timer.Stop();
        if (IsLoaded)
        {
            Close();
        }
        else
        {
            Loaded += (_, _) => Close();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!_done)
        {
            e.Cancel = true;
            if (ViewModel?.CancelCommand.CanExecute(null) == true)
            {
                ViewModel.CancelCommand.Execute(null);
            }
        }

        base.OnClosing(e);
    }
}
