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

    /// <summary>
    /// Lets the list of missing files fill the rest of the window. In a small window it keeps
    /// its minimum height and the page scrolls instead.
    /// </summary>
    private void Page_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // The event also bubbles up from the scroll bars inside the list.
        if (e.OriginalSource != Page || !MissingFilesGrid.IsVisible)
        {
            return;
        }

        var top = MissingFilesGrid.TranslatePoint(default, PageContent).Y;
        var available = Page.ViewportHeight - top - PageContent.Margin.Top - PageContent.Margin.Bottom;

        // Rounded down, so that rounding in the layout cannot bring up the scroll bar.
        MissingFilesGrid.Height = Math.Floor(Math.Max(MissingFilesGrid.MinHeight, available));
    }

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
