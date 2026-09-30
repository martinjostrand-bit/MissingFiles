using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MissingFiles.App.Services;
using MissingFiles.Core.Copying;
using MissingFiles.Core.Scanning;

namespace MissingFiles.App.ViewModels;

/// <summary>A row in the list of missing files (REQ-21).</summary>
public sealed class MissingFileRow(MissingFile file)
{
    public string RelativePath { get; } = file.RelativePath;

    public long SizeBytes { get; } = file.SizeBytes;

    public string SizeText { get; } = Copier.FormatBytes(file.SizeBytes);

    public DateTime LastWriteTime { get; } = file.LastWriteTimeUtc.ToLocalTime();

    public bool SameNameDifferentSize { get; } = file.SameNameDifferentSize;

    public string SameNameDifferentSizeText => SameNameDifferentSize ? "Yes" : string.Empty;
}

/// <summary>
/// The result view (REQ-20, REQ-21, REQ-21a): summary, errors and the list of missing files,
/// which can be filtered and sorted.
/// </summary>
/// <remarks>
/// Filtering and sorting are done here on a plain list, so they stay fast for 100,000+ rows
/// and do not depend on WPF collection views.
/// </remarks>
public sealed partial class ScanResultViewModel : ObservableObject
{
    public const string SortByPath = nameof(MissingFileRow.RelativePath);
    public const string SortBySize = nameof(MissingFileRow.SizeBytes);
    public const string SortByDate = nameof(MissingFileRow.LastWriteTime);
    public const string SortBySameName = nameof(MissingFileRow.SameNameDifferentSize);

    private readonly IShellService _shell;
    private readonly List<MissingFileRow> _allRows;
    private string _sortMember = SortByPath;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    public ScanResultViewModel(ScanResult result, string resultFilePath, IShellService shell)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        ResultFilePath = resultFilePath;
        _shell = shell;
        _allRows = [.. result.MissingFiles.Select(f => new MissingFileRow(f))];
        VisibleMissingFiles = _allRows;
        ApplyView();
    }

    public ScanResult Result { get; }

    public string ResultFilePath { get; }

    public ScanSummary Summary => Result.Summary;

    public string ScanTimeText => string.Create(
        CultureInfo.CurrentCulture,
        $"{Result.ScanStarted.LocalDateTime:g}, took {ProgressViewModel.FormatDuration(Result.Duration)}");

    public IReadOnlyList<ScanError> Errors => Result.Errors;

    public bool HasErrors => Result.Errors.Count > 0;

    public bool HasDuplicateNames => Summary.DuplicateNameGroups > 0;

    /// <summary>The rows after filtering and sorting.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MissingFileRow> VisibleMissingFiles { get; private set; }

    /// <summary>Shows only rows whose relative path contains this text (case-insensitive).</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    public string VisibleCountText => VisibleMissingFiles.Count == _allRows.Count
        ? string.Create(CultureInfo.CurrentCulture, $"{_allRows.Count:N0} files")
        : string.Create(CultureInfo.CurrentCulture, $"Showing {VisibleMissingFiles.Count:N0} of {_allRows.Count:N0} files");

    /// <summary>Sorts the list by a <see cref="MissingFileRow"/> property name.</summary>
    public void Sort(string member, ListSortDirection direction)
    {
        _sortMember = member;
        _sortDirection = direction;
        ApplyView();
    }

    [RelayCommand]
    private void OpenResultFolder() => _shell.ShowInExplorer(ResultFilePath);

    partial void OnFilterTextChanged(string value) => ApplyView();

    partial void OnVisibleMissingFilesChanged(IReadOnlyList<MissingFileRow> value) => OnPropertyChanged(nameof(VisibleCountText));

    private void ApplyView()
    {
        IEnumerable<MissingFileRow> rows = _allRows;
        var filter = FilterText.Trim();
        if (filter.Length > 0)
        {
            rows = rows.Where(r => r.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var list = rows.ToList();
        Comparison<MissingFileRow> compare = _sortMember switch
        {
            SortBySize => (a, b) => a.SizeBytes.CompareTo(b.SizeBytes),
            SortByDate => (a, b) => a.LastWriteTime.CompareTo(b.LastWriteTime),
            SortBySameName => (a, b) => a.SameNameDifferentSize.CompareTo(b.SameNameDifferentSize),
            _ => (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.RelativePath, b.RelativePath),
        };

        // Ties are ordered by path, so the order is always the same.
        list.Sort((a, b) =>
        {
            var result = compare(a, b);
            if (result == 0 && _sortMember != SortByPath)
            {
                result = StringComparer.CurrentCultureIgnoreCase.Compare(a.RelativePath, b.RelativePath);
            }

            return _sortDirection == ListSortDirection.Ascending ? result : -result;
        });

        VisibleMissingFiles = list;
    }
}
