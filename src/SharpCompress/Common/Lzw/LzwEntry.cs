using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Lzw;

public partial class LzwEntry : Entry
{
    private readonly LzwFilePart? _filePart;

    internal LzwEntry(LzwFilePart? filePart) => _filePart = filePart;

    public override CompressionType CompressionType => CompressionType.Lzw;

    public override long Crc => 0;

    public override string? Key => _filePart?.FilePartName;

    public override string? LinkTarget => null;

    public override long CompressedSize => 0;

    public override long Size => 0;

    public override DateTime? LastModifiedTime => null;

    public override DateTime? CreatedTime => null;

    public override DateTime? LastAccessedTime => null;

    public override DateTime? ArchivedTime => null;

    public override bool IsEncrypted => false;

    public override bool IsDirectory => false;

    public override bool IsSplitAfter => false;

    internal override IEnumerable<FilePart> Parts => _filePart.Empty();

    internal static IEnumerable<LzwEntry> GetEntries(Stream stream, OptionsBase options)
    {
        yield return new LzwEntry(LzwFilePart.Create(stream, options.ArchiveEncoding));
    }

    // Async methods moved to LzwEntry.Async.cs
}
