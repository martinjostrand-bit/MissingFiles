namespace MissingFiles.Core.FileTypes;

/// <summary>
/// The file types file could not be read or is not valid (spec REQ-05d).
/// The message names the file and the problem.
/// </summary>
public sealed class FileTypesException : Exception
{
    public FileTypesException()
    {
    }

    public FileTypesException(string message)
        : base(message)
    {
    }

    public FileTypesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private FileTypesException(string fileName, string problem, Exception? innerException)
        : base($"File types file '{fileName}': {problem}", innerException)
    {
        FileName = fileName;
    }

    /// <summary>Path of the file types file, or a description of the built-in default.</summary>
    public string? FileName { get; }

    internal static FileTypesException For(string fileName, string problem, Exception? innerException = null) =>
        new(fileName, problem, innerException);
}
