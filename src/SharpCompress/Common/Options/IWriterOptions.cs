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
    /// Registry of compression providers.
    /// Defaults to <see cref="CompressionProviderRegistry.Default" /> but can be replaced with custom providers, such as
    /// System.IO.Compression for Deflate/GZip on modern .NET.
    /// </summary>
    CompressionProviderRegistry Providers { get; init; }
}
