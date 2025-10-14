using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar;

public class TarEntry : Entry
{
    private readonly TarFilePart? _filePart;

    internal TarEntry(TarFilePart? filePart, CompressionType type)
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

    public long Mode => _filePart?.Header.Mode ?? 0;

    public long UserID => _filePart?.Header.UserId ?? 0;

    public long GroupId => _filePart?.Header.GroupId ?? 0;

    internal override IEnumerable<FilePart> Parts => _filePart.Empty();

    internal static IEnumerable<TarEntry> GetEntries(
        StreamingMode mode,
        Stream stream,
        CompressionType compressionType,
        ArchiveEncoding archiveEncoding
    )
    {
        foreach (var header in TarHeaderFactory.ReadHeader(mode, stream, archiveEncoding))
        {
            if (header != null)
            {
                if (mode == StreamingMode.Seekable)
                {
                    yield return new TarEntry(new TarFilePart(header, stream), compressionType);
                }
                else
                {
                    yield return new TarEntry(new TarFilePart(header, null), compressionType);
                }
            }
            else
            {
                throw new IncompleteArchiveException("Unexpected EOF reading tar file");
            }
        }
    }
}
