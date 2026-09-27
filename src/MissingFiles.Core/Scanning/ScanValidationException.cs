namespace MissingFiles.Core.Scanning;

/// <summary>
/// The source or destination folder cannot be used for a scan (spec REQ-02).
/// </summary>
public sealed class ScanValidationException : Exception
{
    public ScanValidationException()
    {
    }

    public ScanValidationException(string message)
        : base(message)
    {
    }

    public ScanValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
