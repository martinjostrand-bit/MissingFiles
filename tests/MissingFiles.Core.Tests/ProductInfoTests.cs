using MissingFiles.Core;

namespace MissingFiles.Core.Tests;

public class ProductInfoTests
{
    [Fact]
    public void VersionIsSemanticVersionWithoutCommitHash()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+", ProductInfo.Version);
        Assert.DoesNotContain("+", ProductInfo.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayNameContainsNameAndVersion()
    {
        Assert.Equal($"MissingFiles {ProductInfo.Version}", ProductInfo.DisplayName);
    }
}
