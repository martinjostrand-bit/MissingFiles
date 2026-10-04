using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using MissingFiles.App.ViewModels;

namespace MissingFiles.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Use the height from the XAML where it fits, so the list of missing files gets more rows,
        // but never more than the screen, e.g. on a laptop at 150 % scaling.
        Height = Math.Max(MinHeight, Math.Min(Height, SystemParameters.WorkArea.Height - 40));
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>Lets the view model sort the list, which is much faster than WPF sorting for large lists.</summary>
    private void MissingFilesGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (ViewModel?.Result is not { } result)
        {
            return;
        }

        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        foreach (var column in MissingFilesGrid.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = direction;
        result.Sort(e.Column.SortMemberPath, direction);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ViewModel?.SaveSettings();
        base.OnClosing(e);
    }
}
