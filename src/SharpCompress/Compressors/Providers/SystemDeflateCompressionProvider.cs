using System;
using System.IO;
using System.IO.Compression;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Providers;

/// <summary>
/// Provides Deflate compression using System.IO.Compression.DeflateStream.
/// </summary>
/// <remarks>
/// On modern .NET (5+), System.IO.Compression uses hardware-accelerated zlib
/// and is significantly faster than SharpCompress's pure C# implementation.
/// </remarks>
public sealed class SystemDeflateCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Deflate;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var bclLevel = MapCompressionLevel(compressionLevel);
        return new DeflateStream(destination, bclLevel, leaveOpen: true);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new DeflateStream(
            source,
            System.IO.Compression.CompressionMode.Decompress,
            leaveOpen: true
        );
    }

    /// <summary>
    /// Maps SharpCompress compression level (0-9) to BCL CompressionLevel.
    /// </summary>
    private static System.IO.Compression.CompressionLevel MapCompressionLevel(int level)
    {
        // Map 0-9 to appropriate BCL levels
        return level switch
        {
            0 => System.IO.Compression.CompressionLevel.NoCompression,
            <= 2 => System.IO.Compression.CompressionLevel.Fastest,
#if NET7_0_OR_GREATER
            >= 8 => System.IO.Compression.CompressionLevel.SmallestSize,
#endif
            _ => System.IO.Compression.CompressionLevel.Optimal,
        };
    }
}
