using System.Globalization;
using System.Reflection;

using MissingFiles.Core;

namespace MissingFiles.Core.Tests;

public class ProductInfoTests
{
    [Fact]
    public void VersionHasFourNumbersWithinTheWindowsLimit() // CM-01
    {
        var numbers = ProductInfo.Version.Split('.');

        Assert.Equal(4, numbers.Length);
        Assert.All(numbers, number =>
        {
            Assert.Matches("^(0|[1-9][0-9]*)$", number);
            Assert.InRange(int.Parse(number, CultureInfo.InvariantCulture), 0, 65534);
        });
    }

    [Fact]
    public void RevisionStaysAtOneZeroZeroUntilTheFirstRelease() // CM-03
    {
        // Change this together with Directory.Build.props when the first release has been published.
        Assert.StartsWith("1.0.0.", ProductInfo.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void AllVersionAttributesOfTheAssemblyHaveTheFullRevision() // CM-05
    {
        var assembly = typeof(ProductInfo).Assembly;

        Assert.Equal(ProductInfo.Version, assembly.GetName().Version?.ToString());
        Assert.Equal(ProductInfo.Version, assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
        Assert.Equal(ProductInfo.Version, assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
    }

    [Fact]
    public void DisplayNameContainsNameAndVersion()
    {
        Assert.Equal($"MissingFiles {ProductInfo.Version}", ProductInfo.DisplayName);
    }
}
