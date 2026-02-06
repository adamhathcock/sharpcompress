using SharpCompress.Common;
using SharpCompress.Compressors;

namespace SharpCompress.Common.Options;

/// <summary>
/// Options for configuring writer behavior when creating archives.
/// </summary>
public interface IWriterOptions : IStreamOptions, IEncodingOptions, IProgressOptions
{
    /// <summary>
    /// The compression type to use for the archive.
    /// </summary>
    CompressionType CompressionType { get; init; }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// </summary>
    int CompressionLevel { get; init; }

    /// <summary>
    /// Optional registry of compression providers.
    /// If null, the default registry (SharpCompress internal implementations) will be used.
    /// Use this to provide alternative compression implementations, such as
    /// System.IO.Compression for Deflate/GZip on modern .NET.
    /// </summary>
    CompressionProviderRegistry? CompressionProviders { get; init; }
}
