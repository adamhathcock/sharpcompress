using System;
using System.Collections.Generic;
using System.Linq;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Common.Zip.SOZip;

namespace SharpCompress.Common.Zip;

public class ZipEntry : Entry
{
    private readonly ZipFilePart? _filePart;

    internal ZipEntry(ZipFilePart? filePart)
    {
        if (filePart is null)
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
            ZipCompressionMethod.ZStandard => CompressionType.ZStandard,
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

    public override int? Attrib => (int?)_filePart?.Header.ExternalFileAttributes;

    public string? Comment => _filePart?.Header.Comment;

    /// <summary>
    /// Gets a value indicating whether this entry has SOZip (Seek-Optimized ZIP) support.
    /// A SOZip entry has an associated index file that enables random access within
    /// the compressed data.
    /// </summary>
    public bool IsSozip => _filePart?.Header.Extra.Any(e => e.Type == ExtraDataType.SOZip) ?? false;

    /// <summary>
    /// Gets a value indicating whether this entry is a SOZip index file.
    /// Index files are hidden files with a .sozip.idx extension that contain
    /// offsets into the main compressed file.
    /// </summary>
    public bool IsSozipIndexFile => Key is not null && SOZipIndex.IsIndexFile(Key);

    /// <summary>
    /// Gets the SOZip extra field data, if present.
    /// </summary>
    internal SOZipExtraField? SOZipExtra =>
        _filePart?.Header.Extra.OfType<SOZipExtraField>().FirstOrDefault();
}
