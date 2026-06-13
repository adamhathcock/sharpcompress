using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Providers;

namespace SharpCompress.Common.Options;

/// <summary>
/// Options for configuring writer behavior when creating archives.
/// </summary>
public interface IWriterOptions : IStreamOptions, IEncodingOptions, IProgressOptions
{
    /// <summary>
    /// The compression type to use for the archive.
    /// </summary>
    CompressionType CompressionType { get; set; }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// </summary>
    int CompressionLevel { get; set; }

    /// <summary>
    /// Buffer size for writer stream copy operations.
    /// </summary>
    int BufferSize { get; set; }

    /// <summary>
    /// Registry of compression providers.
    /// Defaults to <see cref="CompressionProviderRegistry.Default" /> but can be replaced with custom providers, such as
    /// System.IO.Compression for Deflate/GZip on modern .NET.
    /// </summary>
    CompressionProviderRegistry Providers { get; set; }
}
