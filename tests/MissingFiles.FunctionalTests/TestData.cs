using System.Globalization;

namespace MissingFiles.FunctionalTests;

/// <summary>
/// Test data generator (spec FT-01): realistic camera, phone and video file names, and tiny
/// dummy files (at most <see cref="MaxFileSize"/> bytes) whose content depends only on the
/// file name and size, so a file and its copy, or the same photo in two folders, are identical.
/// </summary>
internal static class TestData
{
    public const int MaxFileSize = 1024;

    // Name patterns of common cameras and phones; all extensions are in the default file types file.
    private static readonly Func<int, string>[] Patterns =
    [
        i => $"IMG_{i:D4}.JPG",                                       // Canon, iPhone
        i => $"DSC{4500 + i:D5}.ARW",                                 // Sony raw
        i => $"VID_{Taken(i):yyyyMMdd_HHmmss}.mp4",                    // Android video
        i => $"PXL_{Taken(i):yyyyMMdd_HHmmssfff}.jpg",                 // Pixel
        i => $"IMG_{i:D4}.HEIC",                                      // iPhone
        i => $"_MG_{i:D4}.CR2",                                       // Canon raw
        i => $"DSCN{i:D4}.NEF",                                       // Nikon raw
        i => $"MVI_{i:D4}.MOV",                                       // Canon video
        i => $"P{1000000 + i:D7}.RW2",                                // Panasonic raw
        i => $"GOPR{i:D4}.MP4",                                       // GoPro
    ];

    private static readonly string[] Folders =
    [
        @"DCIM\100CANON",
        @"DCIM\101CANON",
        @"Phone\Camera",
        @"2021\Summer holiday",
        @"2021\Christmas",
        @"2022\Bjørn's birthday",
        "Unsorted",
    ];

    /// <summary>
    /// <paramref name="count"/> distinct file names, e.g. <c>IMG_0001.JPG</c>, <c>DSC04502.ARW</c>,
    /// <c>VID_20210103_100203.mp4</c>. The same <paramref name="first"/> gives the same names.
    /// </summary>
    public static IReadOnlyList<string> Names(int count, int first = 1) =>
        [.. Enumerable.Range(first, count).Select(i => Patterns[i % Patterns.Length](i))];

    /// <summary>Names placed in camera and archive folders, e.g. <c>DCIM\100CANON\IMG_0001.JPG</c>.</summary>
    public static IReadOnlyList<string> RelativePaths(int count, int first = 1) =>
        [.. Names(count, first).Select((name, n) => Path.Join(Folders[(first + n) % Folders.Length], name))];

    /// <summary>A size between 1 and <see cref="MaxFileSize"/> bytes that depends only on the file name.</summary>
    public static int SizeOf(string relativePath) => 1 + (Hash(Path.GetFileName(relativePath)) % MaxFileSize);

    /// <summary>File content that depends only on the file name (case-insensitive) and size.</summary>
    public static byte[] Content(string relativePath, int size)
    {
        var bytes = new byte[size];
        new Random(Hash(Path.GetFileName(relativePath)) ^ size).NextBytes(bytes);
        return bytes;
    }

    /// <summary>A different time for every index, so names with a timestamp are unique.</summary>
    private static DateTime Taken(int i) => new DateTime(2021, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddSeconds(i * 37.001);

    /// <summary>A stable hash (string.GetHashCode differs between runs).</summary>
    private static int Hash(string fileName)
    {
        var hash = 17;
        foreach (var c in fileName.ToUpperInvariant())
        {
            hash = unchecked((hash * 31) + c);
        }

        return hash & int.MaxValue;
    }
}
