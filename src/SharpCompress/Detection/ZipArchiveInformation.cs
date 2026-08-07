namespace SharpCompress.Detection;

/// <summary>
/// Contains ZIP-specific metadata.
/// </summary>
public sealed class ZipArchiveInformation
{
    internal ZipArchiveInformation(bool hasEntriesWithDeferredSizes) =>
        HasEntriesWithDeferredSizes = hasEntriesWithDeferredSizes;

    /// <summary>
    /// Gets whether any entry sizes are available only after reading the entry data.
    /// When <see langword="true"/>, a forward-only reader can initially report zero sizes for those entries.
    /// </summary>
    public bool HasEntriesWithDeferredSizes { get; }
}
