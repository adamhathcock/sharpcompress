using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.GZip;

public partial class GZipEntry : Entry
{
    private readonly GZipFilePart? _filePart;

    internal GZipEntry(GZipFilePart? filePart) => _filePart = filePart;

    public override CompressionType CompressionType => CompressionType.GZip;

    public override long Crc => _filePart?.Crc ?? 0;

    public override string? Key => _filePart?.FilePartName;

    public override string? LinkTarget => null;

    public override long CompressedSize => 0;

    public override long Size => _filePart?.UncompressedSize ?? 0;

    public override DateTime? LastModifiedTime => _filePart?.DateModified;

    public override DateTime? CreatedTime => null;

    public override DateTime? LastAccessedTime => null;

    public override DateTime? ArchivedTime => null;

    public override bool IsEncrypted => false;

    public override bool IsDirectory => false;

    public override bool IsSplitAfter => false;

    internal override IEnumerable<FilePart> Parts => _filePart.Empty();

    internal static IEnumerable<GZipEntry> GetEntries(Stream stream, ReaderOptions options)
    {
        yield return new GZipEntry(
            GZipFilePart.Create(stream, options.ArchiveEncoding, options.CompressionProviders)
        );
    }

    // Async methods moved to GZipEntry.Async.cs
}
