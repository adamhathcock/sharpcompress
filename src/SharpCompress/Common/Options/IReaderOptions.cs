using SharpCompress.Compressors;

namespace SharpCompress.Common.Options;

public interface IReaderOptions
    : IStreamOptions,
        IEncodingOptions,
        IProgressOptions,
        IExtractionOptions
{
    /// <summary>
    /// Look for RarArchive (Check for self-extracting archives or cases where RarArchive isn't at the start of the file)
    /// </summary>
    bool LookForHeader { get; init; }

    /// <summary>
    /// Password for encrypted archives.
    /// </summary>
    string? Password { get; init; }

    /// <summary>
    /// Disable checking for incomplete archives.
    /// </summary>
    bool DisableCheckIncomplete { get; init; }

    /// <summary>
    /// Buffer size for stream operations.
    /// </summary>
    int BufferSize { get; init; }

    /// <summary>
    /// Provide a hint for the extension of the archive being read, can speed up finding the correct decoder.
    /// </summary>
    string? ExtensionHint { get; init; }

    /// <summary>
    /// Size of the rewindable buffer for non-seekable streams.
    /// </summary>
    int? RewindableBufferSize { get; init; }

    /// <summary>
    /// Registry of compression providers.
    /// Defaults to <see cref="CompressionProviderRegistry.Default" /> but can be replaced with custom providers.
    /// Use this to provide alternative decompression implementations.
    /// </summary>
    CompressionProviderRegistry CompressionProviders { get; init; }
}
