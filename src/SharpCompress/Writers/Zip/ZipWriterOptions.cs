using System;
using SharpCompress.Common;
using SharpCompress.Common.Zip.SOZip;
using SharpCompress.Compressors.Deflate;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip;

public class ZipWriterOptions : WriterOptions
{
    public ZipWriterOptions(
        CompressionType compressionType,
        CompressionLevel compressionLevel = D.CompressionLevel.Default
    )
        : base(compressionType, (int)compressionLevel) { }

    internal ZipWriterOptions(WriterOptions options)
        : base(options.CompressionType)
    {
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        CompressionLevel = options.CompressionLevel;

        if (options is ZipWriterOptions writerOptions)
        {
            UseZip64 = writerOptions.UseZip64;
            ArchiveComment = writerOptions.ArchiveComment;
            EnableSOZip = writerOptions.EnableSOZip;
            SOZipChunkSize = writerOptions.SOZipChunkSize;
            SOZipMinFileSize = writerOptions.SOZipMinFileSize;
        }
    }

    /// <summary>
    /// Sets the compression level for Deflate compression (0-9).
    /// This is a convenience method that sets the CompressionLevel property for Deflate compression.
    /// </summary>
    /// <param name="level">Deflate compression level (0=no compression, 6=default, 9=best compression)</param>
    public void SetDeflateCompressionLevel(CompressionLevel level)
    {
        CompressionLevel = (int)level;
    }

    /// <summary>
    /// Sets the compression level for ZStandard compression (1-22).
    /// This is a convenience method that sets the CompressionLevel property for ZStandard compression.
    /// </summary>
    /// <param name="level">ZStandard compression level (1=fastest, 3=default, 22=best compression)</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when level is not between 1 and 22</exception>
    public void SetZStandardCompressionLevel(int level)
    {
        if (level < 1 || level > 22)
            throw new ArgumentOutOfRangeException(
                nameof(level),
                "ZStandard compression level must be between 1 and 22"
            );

        CompressionLevel = level;
    }

    /// <summary>
    /// Legacy property for Deflate compression levels.
    /// Valid range: 0-9 (0=no compression, 6=default, 9=best compression).
    /// </summary>
    /// <remarks>
    /// This property is deprecated. Use <see cref="WriterOptions.CompressionLevel"/> or <see cref="SetDeflateCompressionLevel"/> instead.
    /// </remarks>
    [Obsolete(
        "Use CompressionLevel property or SetDeflateCompressionLevel method instead. This property will be removed in a future version."
    )]
    public CompressionLevel DeflateCompressionLevel
    {
        get => (CompressionLevel)Math.Min(CompressionLevel, 9);
        set => CompressionLevel = (int)value;
    }

    public string? ArchiveComment { get; set; }

    /// <summary>
    /// Sets a value indicating if zip64 support is enabled.
    /// If this is not set, individual stream lengths cannot exceed 4 GiB.
    /// This option is not supported for non-seekable streams.
    /// Archives larger than 4GiB are supported as long as all streams
    /// are less than 4GiB in length.
    /// </summary>
    public bool UseZip64 { get; set; }

    /// <summary>
    /// Enables SOZip (Seek-Optimized ZIP) for Deflate-compressed files.
    /// When enabled, files that meet the minimum size requirement will have
    /// an accompanying index file that allows random access within the
    /// compressed data. Requires a seekable output stream.
    /// </summary>
    public bool EnableSOZip { get; set; }

    /// <summary>
    /// The chunk size for SOZip index creation in bytes.
    /// Must be a multiple of 1024 bytes. Default is 32KB (32768 bytes).
    /// Smaller chunks allow for finer-grained random access but result
    /// in larger index files and slightly less efficient compression.
    /// </summary>
    public int SOZipChunkSize { get; set; } = (int)SOZipIndex.DEFAULT_CHUNK_SIZE;

    /// <summary>
    /// Minimum file size (uncompressed) in bytes for SOZip optimization.
    /// Files smaller than this size will not have SOZip index files created.
    /// Default is 1MB (1048576 bytes).
    /// </summary>
    public long SOZipMinFileSize { get; set; } = 1048576;
}
