using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>
/// Identifies an archive format without enumerating its entries.
/// </summary>
public sealed class ArchiveDetection
{
    internal ArchiveDetection(
        ArchiveType? containerType,
        string formatName,
        CompressionType? outerCompressionType,
        ArchiveAccessMode supportedApis
    )
    {
        ContainerType = containerType;
        FormatName = formatName;
        OuterCompressionType = outerCompressionType;
        SupportedApis = supportedApis;
    }

    /// <summary>
    /// Gets the logical archive container type, when it is a built-in SharpCompress type.
    /// </summary>
    public ArchiveType? ContainerType { get; }

    /// <summary>
    /// Gets the detected format name.
    /// </summary>
    public string FormatName { get; }

    /// <summary>
    /// Gets the compression wrapper around <see cref="ContainerType"/>, if present.
    /// For example, a tar.xz archive has a TAR container and XZ outer compression.
    /// </summary>
    public CompressionType? OuterCompressionType { get; }

    /// <summary>
    /// Gets the APIs supported by the detected format.
    /// </summary>
    public ArchiveAccessMode SupportedApis { get; }
}
