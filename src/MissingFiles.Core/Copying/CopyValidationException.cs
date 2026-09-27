namespace MissingFiles.Core.Copying;

/// <summary>
/// A copy cannot start: a folder is missing or not usable, or there is not enough free space.
/// </summary>
public sealed class CopyValidationException : Exception
{
    public CopyValidationException()
    {
    }

    public CopyValidationException(string message)
        : base(message)
    {
    }

    public CopyValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
