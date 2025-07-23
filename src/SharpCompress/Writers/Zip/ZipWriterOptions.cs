using System;
using SharpCompress.Common;
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
}
