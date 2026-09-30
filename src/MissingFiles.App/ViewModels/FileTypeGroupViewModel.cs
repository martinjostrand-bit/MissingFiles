using CommunityToolkit.Mvvm.ComponentModel;

using MissingFiles.Core.FileTypes;

namespace MissingFiles.App.ViewModels;

/// <summary>A group from the file types file, shown as a checkbox (REQ-06).</summary>
public sealed partial class FileTypeGroupViewModel(FileTypeGroup group) : ObservableObject
{
    public string Name { get; } = group.Name;

    /// <summary>The extensions of the group, e.g. ".jpg .jpeg .png", or "(no extensions)".</summary>
    public string ExtensionsText { get; } = group.Extensions.Count == 0
        ? "(no extensions)"
        : string.Join(' ', group.Extensions.Select(e => e switch
        {
            ExtensionFilter.AllFiles => "all files",
            ExtensionFilter.NoExtension => "(no extension)",
            _ => e,
        }));

    /// <summary>Initially the <c>enabled</c> flag from the file; the user can change it for one scan.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; } = group.Enabled;
}
