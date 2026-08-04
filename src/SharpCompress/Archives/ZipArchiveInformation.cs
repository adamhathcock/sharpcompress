namespace SharpCompress.Archives;

/// <summary>
/// Contains ZIP-specific metadata.
/// </summary>
public sealed class ZipArchiveInformation
{
    internal ZipArchiveInformation(long dataDescriptorEntryCount) =>
        DataDescriptorEntryCount = dataDescriptorEntryCount;

    /// <summary>
    /// Gets the number of entries whose local header defers its CRC and sizes to a data descriptor.
    /// </summary>
    public long DataDescriptorEntryCount { get; }
}
