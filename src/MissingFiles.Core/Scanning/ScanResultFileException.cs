namespace MissingFiles.Core.Scanning;

/// <summary>
/// A scan result file could not be read or is not valid.
/// </summary>
public sealed class ScanResultFileException : Exception
{
    public ScanResultFileException()
    {
    }

    public ScanResultFileException(string message)
        : base(message)
    {
    }

    public ScanResultFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
