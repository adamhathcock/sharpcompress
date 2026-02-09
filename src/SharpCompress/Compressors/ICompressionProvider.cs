using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Compressors;

/// <summary>
/// Provides compression and decompression stream creation for a specific compression type.
/// Implement this interface to supply alternative compression implementations.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the creation of compression and decompression streams,
/// allowing SharpCompress to use different implementations of the same compression type.
/// For example, you can provide an implementation that uses System.IO.Compression
/// for Deflate/GZip instead of the internal DotNetZip-derived implementation.
/// </para>
/// <para>
/// Implementations should be thread-safe for concurrent decompression operations,
/// but CreateCompressStream/CreateDecompressStream themselves return new stream instances
/// that are not shared.
/// </para>
/// </remarks>
public interface ICompressionProvider
{
    /// <summary>
    /// The compression type this provider handles.
    /// </summary>
    CompressionType CompressionType { get; }

    /// <summary>
    /// Whether this provider supports compression (writing).
    /// </summary>
    bool SupportsCompression { get; }

    /// <summary>
    /// Whether this provider supports decompression (reading).
    /// </summary>
    bool SupportsDecompression { get; }

    /// <summary>
    /// Creates a compression stream that compresses data written to it.
    /// </summary>
    /// <param name="destination">The destination stream to write compressed data to.</param>
    /// <param name="compressionLevel">The compression level (0-9, algorithm-specific).</param>
    /// <returns>A stream that compresses data written to it.</returns>
    /// <exception cref="NotSupportedException">Thrown if SupportsCompression is false.</exception>
    Stream CreateCompressStream(Stream destination, int compressionLevel);

    /// <summary>
    /// Creates a compression stream with context information.
    /// </summary>
    /// <param name="destination">The destination stream.</param>
    /// <param name="compressionLevel">The compression level.</param>
    /// <param name="context">Context information about the compression.</param>
    /// <returns>A compression stream.</returns>
    /// <exception cref="NotSupportedException">Thrown if SupportsCompression is false.</exception>
    Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    );

    /// <summary>
    /// Creates a decompression stream that decompresses data read from it.
    /// </summary>
    /// <param name="source">The source stream to read compressed data from.</param>
    /// <returns>A stream that decompresses data read from it.</returns>
    /// <exception cref="NotSupportedException">Thrown if SupportsDecompression is false.</exception>
    Stream CreateDecompressStream(Stream source);

    /// <summary>
    /// Creates a decompression stream with context information.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="context">Context information about the decompression.</param>
    /// <returns>A decompression stream.</returns>
    /// <exception cref="NotSupportedException">Thrown if SupportsDecompression is false.</exception>
    Stream CreateDecompressStream(Stream source, CompressionContext context);
}
