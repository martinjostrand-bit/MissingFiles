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

    [Fact]
    public void AboutItemsHaveTheValuesOfTheSpecification() // REQ-36
    {
        Assert.Equal("MissingFiles", ProductInfo.Name);
        Assert.Equal("Martin Strand", ProductInfo.Author);
        Assert.Equal("Copyright © 2026 Martin Strand", ProductInfo.Copyright);
        Assert.Equal("Free to use.", ProductInfo.LicenseStatement);
    }

    [Fact]
    public void AboutTextHasAllItemsInOrderFollowedByTheFullLicenseText() // REQ-36
    {
        var expected = string.Join(
            Environment.NewLine,
            "MissingFiles",
            $"Revision: {ProductInfo.Version}",
            "Author: Martin Strand",
            "Copyright © 2026 Martin Strand",
            string.Empty,
            "License: Free to use.",
            string.Empty,
            ProductInfo.LicenseText);

        Assert.Equal(expected, ProductInfo.AboutText);
    }

    [Fact]
    public void LicenseTextIsTheEmbeddedLicenseFile() // REQ-36, REQ-39
    {
        var file = File.ReadAllText(Path.Join(RepositoryRoot(), "LICENSE")).ReplaceLineEndings().TrimEnd();

        Assert.Equal(file, ProductInfo.LicenseText);
        Assert.Contains("MissingFiles.Core.LICENSE", typeof(ProductInfo).Assembly.GetManifestResourceNames());
    }

    [Fact]
    public void LicenseIsTheFullMitLicenseOfTheAuthor() // REQ-36
    {
        var lines = ProductInfo.LicenseText.Split(Environment.NewLine);

        Assert.Equal("MIT License", lines[0]);
        Assert.Equal("Copyright (c) 2026 Martin Strand", lines[2]);
        Assert.Contains("Permission is hereby granted, free of charge, to any person obtaining a copy", lines);
        Assert.Contains("The above copyright notice and this permission notice shall be included in all", lines);
        Assert.Equal("SOFTWARE.", lines[^1]);
        Assert.DoesNotContain(lines, line => line.EndsWith(' ') || line.Contains('\r', StringComparison.Ordinal) || line.Contains('\n', StringComparison.Ordinal));
    }

    [Fact]
    public void AssemblyCopyrightIsTheAboutCopyright() // REQ-36: the file properties show the same
    {
        Assert.Equal(ProductInfo.Copyright, typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright);
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Join(folder.FullName, "MissingFiles.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No repository root above {AppContext.BaseDirectory}.");
    }
}
