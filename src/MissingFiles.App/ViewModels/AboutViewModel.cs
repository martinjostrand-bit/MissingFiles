using MissingFiles.Core;

namespace MissingFiles.App.ViewModels;

/// <summary>
/// The About dialog (REQ-36, REQ-37). Everything comes from the core library, so the dialog
/// and <c>--about</c> show the same (REQ-39).
/// </summary>
public sealed class AboutViewModel
{
    public string Title { get; } = $"About {ProductInfo.Name}";

    /// <summary>Name, revision, author, copyright, license statement and the full license text.</summary>
    public string Text { get; } = ProductInfo.AboutText;
}
