using System.IO;
using System.IO.Compression;
using SharpCompress.Common;

namespace SharpCompress.Providers.System;

/// <summary>
/// Provides Deflate compression using System.IO.Compression.DeflateStream.
/// </summary>
/// <remarks>
/// On modern .NET (5+), System.IO.Compression uses hardware-accelerated zlib
/// and is significantly faster than SharpCompress's pure C# implementation.
/// </remarks>
public sealed class SystemDeflateCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Deflate;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var bclLevel = MapCompressionLevel(compressionLevel);
        return new DeflateStream(destination, bclLevel, leaveOpen: false);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new DeflateStream(
            source,
            global::System.IO.Compression.CompressionMode.Decompress,
            leaveOpen: false
        );
    }

    /// <summary>
    /// Maps SharpCompress compression level (0-9) to BCL CompressionLevel.
    /// </summary>
    private static global::System.IO.Compression.CompressionLevel MapCompressionLevel(int level)
    {
        // Map 0-9 to appropriate BCL levels
        return level switch
        {
            0 => global::System.IO.Compression.CompressionLevel.NoCompression,
            <= 2 => global::System.IO.Compression.CompressionLevel.Fastest,
#if NET7_0_OR_GREATER
            >= 8 => global::System.IO.Compression.CompressionLevel.SmallestSize,
#endif
            _ => global::System.IO.Compression.CompressionLevel.Optimal,
        };
    }
}
