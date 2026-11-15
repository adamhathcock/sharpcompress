using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Options;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar;

public partial class TarEntry : Entry
{
    private readonly TarFilePart? _filePart;

    internal TarEntry(TarFilePart? filePart, CompressionType type, IReaderOptions readerOptions)
        : base(readerOptions)
    {
        _filePart = filePart;
        CompressionType = type;
    }

    public override CompressionType CompressionType { get; }

    public override long Crc => 0;

    public override string? Key => _filePart?.Header.Name;

    public override string? LinkTarget => _filePart?.Header.LinkName;

    public override long CompressedSize => _filePart?.Header.Size ?? 0;

    public override long Size => _filePart?.Header.Size ?? 0;

    public override DateTime? LastModifiedTime => _filePart?.Header.LastModifiedTime;

    public override DateTime? CreatedTime => null;

    public override DateTime? LastAccessedTime => null;

    public override DateTime? ArchivedTime => null;

    public override bool IsEncrypted => false;

    public override bool IsDirectory => _filePart?.Header.EntryType == EntryType.Directory;

    public override bool IsSplitAfter => false;

    // Preserve the legacy public directory marker without mixing it into the raw
    // header mode, where 01000 is the real sticky bit and must round-trip unchanged.
    public long Mode => _filePart is null ? 0 : Metadata.GetLegacyMode(IsDirectory);

    internal TarEntryMetadata Metadata => _filePart?.Header.Metadata ?? default;

    public long UserID => Metadata.UserId ?? 0;

    public long GroupId => Metadata.GroupId ?? 0;

    internal override IEnumerable<FilePart> Parts => _filePart.Empty();

    internal static IEnumerable<TarEntry> GetEntries(
        StreamingMode mode,
        Stream stream,
        CompressionType compressionType,
        IArchiveEncoding archiveEncoding,
        IReaderOptions readerOptions
    )
    {
        foreach (var header in TarHeaderFactory.ReadHeader(mode, stream, archiveEncoding))
        {
            if (header != null)
            {
                if (mode == StreamingMode.Seekable)
                {
                    yield return new TarEntry(
                        new TarFilePart(header, stream),
                        compressionType,
                        readerOptions
                    );
                }
                else
                {
                    yield return new TarEntry(
                        new TarFilePart(header, null),
                        compressionType,
                        readerOptions
                    );
                }
            }
            else
            {
                throw new IncompleteArchiveException("Unexpected EOF reading tar file");
            }
        }
    }

    // Async methods moved to TarEntry.Async.cs
}
