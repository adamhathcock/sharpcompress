using System.IO;
using System.IO.Compression;
using SharpCompress.Common;

namespace SharpCompress.Providers.System;

/// <summary>
/// Provides GZip compression using System.IO.Compression.GZipStream.
/// </summary>
/// <remarks>
/// On modern .NET (5+), System.IO.Compression uses hardware-accelerated zlib
/// and is significantly faster than SharpCompress's pure C# implementation.
/// </remarks>
public sealed class SystemGZipCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.GZip;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var bclLevel = MapCompressionLevel(compressionLevel);
        return new GZipStream(destination, bclLevel, leaveOpen: false);
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for simple GZip compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new GZipStream(
            source,
            global::System.IO.Compression.CompressionMode.Decompress,
            leaveOpen: false
        );
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for simple GZip decompression
        return CreateDecompressStream(source);
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
