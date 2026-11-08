using MissingFiles.App.ViewModels;
using MissingFiles.Core;

namespace MissingFiles.App.Tests;

public class AboutViewModelTests
{
    [Fact]
    public void ShowsTheAboutInformationOfTheCoreLibrary() // REQ-36, REQ-39
    {
        var about = new AboutViewModel();

        Assert.Equal("About MissingFiles", about.Title);
        Assert.Equal(ProductInfo.AboutText, about.Text);
        Assert.Contains(ProductInfo.Version, about.Text, StringComparison.Ordinal);
        Assert.Contains("Martin Strand", about.Text, StringComparison.Ordinal);
        Assert.Contains("Copyright © 2026 Martin Strand", about.Text, StringComparison.Ordinal);
        Assert.Contains("Free to use.", about.Text, StringComparison.Ordinal);
        Assert.EndsWith(ProductInfo.LicenseText, about.Text, StringComparison.Ordinal);
    }
}
