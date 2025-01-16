using System;
using System.Collections.Generic;
using System.Linq;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Common.Zip;

public class ZipEntry : Entry
{
    private readonly ZipFilePart? _filePart;

    internal ZipEntry(ZipFilePart? filePart)
    {
        if (filePart == null)
        {
            return;
        }
        _filePart = filePart;

        LastModifiedTime = Utility.DosDateToDateTime(
            filePart.Header.LastModifiedDate,
            filePart.Header.LastModifiedTime
        );

        var times =
            filePart.Header.Extra.FirstOrDefault(header =>
                header.GetType() == typeof(UnixTimeExtraField)
            ) as UnixTimeExtraField;

        LastAccessedTime = times?.UnicodeTimes.Item2;
        CreatedTime = times?.UnicodeTimes.Item3;
    }

    public override CompressionType CompressionType =>
        _filePart?.Header.CompressionMethod switch
        {
            ZipCompressionMethod.BZip2 => CompressionType.BZip2,
            ZipCompressionMethod.Deflate => CompressionType.Deflate,
            ZipCompressionMethod.Deflate64 => CompressionType.Deflate64,
            ZipCompressionMethod.LZMA => CompressionType.LZMA,
            ZipCompressionMethod.PPMd => CompressionType.PPMd,
            ZipCompressionMethod.None => CompressionType.None,
            ZipCompressionMethod.Shrink => CompressionType.Shrink,
            ZipCompressionMethod.Reduce1 => CompressionType.Reduce1,
            ZipCompressionMethod.Reduce2 => CompressionType.Reduce2,
            ZipCompressionMethod.Reduce3 => CompressionType.Reduce3,
            ZipCompressionMethod.Reduce4 => CompressionType.Reduce4,
            ZipCompressionMethod.Explode => CompressionType.Explode,
            _ => CompressionType.Unknown,
        };

    public override long Crc => _filePart?.Header.Crc ?? 0;

    public override string? Key => _filePart?.Header.Name;

    public override string? LinkTarget => null;

    public override long CompressedSize => _filePart?.Header.CompressedSize ?? 0;

    public override long Size => _filePart?.Header.UncompressedSize ?? 0;

    public override DateTime? LastModifiedTime { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// The returned time is UTC, not local.
    /// </remarks>
    public override DateTime? CreatedTime { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// The returned time is UTC, not local.
    /// </remarks>
    public override DateTime? LastAccessedTime { get; }

    public override DateTime? ArchivedTime => null;

    public override bool IsEncrypted =>
        FlagUtility.HasFlag(_filePart?.Header.Flags ?? HeaderFlags.None, HeaderFlags.Encrypted);

    public override bool IsDirectory => _filePart?.Header.IsDirectory ?? false;

    public override bool IsSplitAfter => false;

    internal override IEnumerable<FilePart> Parts => _filePart.Empty();
}
