namespace SharpCompress.Common;

/// <summary>
/// Represents progress information for compression operations.
/// </summary>
public sealed class CompressionProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompressionProgress"/> class.
    /// </summary>
    /// <param name="entryPath">The path of the entry being compressed.</param>
    /// <param name="bytesRead">Number of bytes read from the source.</param>
    /// <param name="totalBytes">Total bytes to be read from the source, or null if unknown.</param>
    public CompressionProgress(string entryPath, long bytesRead, long? totalBytes)
    {
        EntryPath = entryPath;
        BytesRead = bytesRead;
        TotalBytes = totalBytes;
    }

    /// <summary>
    /// Gets the path of the entry being compressed.
    /// </summary>
    public string EntryPath { get; }

    /// <summary>
    /// Gets the number of bytes read from the source so far.
    /// </summary>
    public long BytesRead { get; }

    /// <summary>
    /// Gets the total number of bytes to be read from the source, or null if unknown.
    /// </summary>
    public long? TotalBytes { get; }

    /// <summary>
    /// Gets the progress percentage (0-100), or null if total bytes is unknown.
    /// </summary>
    public double? PercentComplete =>
        TotalBytes.HasValue && TotalBytes.Value > 0
            ? (double)BytesRead / TotalBytes.Value * 100
            : null;
}
