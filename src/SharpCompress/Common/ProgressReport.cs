namespace SharpCompress.Common;

/// <summary>
/// Represents progress information for compression or extraction operations.
/// </summary>
public sealed class ProgressReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressReport"/> class.
    /// </summary>
    /// <param name="entryPath">The path of the entry being processed.</param>
    /// <param name="bytesTransferred">Number of bytes transferred so far.</param>
    /// <param name="totalBytes">Total bytes to be transferred, or null if unknown.</param>
    public ProgressReport(string entryPath, long bytesTransferred, long? totalBytes)
    {
        EntryPath = entryPath;
        BytesTransferred = bytesTransferred;
        TotalBytes = totalBytes;
    }

    /// <summary>
    /// Gets the path of the entry being processed.
    /// </summary>
    public string EntryPath { get; }

    /// <summary>
    /// Gets the number of bytes transferred so far.
    /// </summary>
    public long BytesTransferred { get; }

    /// <summary>
    /// Gets the total number of bytes to be transferred, or null if unknown.
    /// </summary>
    public long? TotalBytes { get; }

    /// <summary>
    /// Gets the progress percentage (0-100), or null if total bytes is unknown.
    /// </summary>
    public double? PercentComplete =>
        TotalBytes.HasValue && TotalBytes.Value > 0
            ? (double)BytesTransferred / TotalBytes.Value * 100
            : null;
}
