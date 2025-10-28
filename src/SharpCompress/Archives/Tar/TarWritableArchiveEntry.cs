using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives.Tar;

internal sealed class TarWritableArchiveEntry : TarArchiveEntry, IWritableArchiveEntry
{
    private readonly bool closeStream;
    private readonly Stream? stream;
    private readonly bool isDirectory;

    internal TarWritableArchiveEntry(
        TarArchive archive,
        Stream stream,
        CompressionType compressionType,
        string path,
        long size,
        DateTime? lastModified,
        bool closeStream
    )
        : base(archive, null, compressionType)
    {
        this.stream = stream;
        Key = path;
        Size = size;
        LastModifiedTime = lastModified;
        this.closeStream = closeStream;
        isDirectory = false;
    }

    internal TarWritableArchiveEntry(
        TarArchive archive,
        string directoryPath,
        DateTime? lastModified
    )
        : base(archive, null, CompressionType.None)
    {
        stream = null;
        Key = directoryPath;
        Size = 0;
        LastModifiedTime = lastModified;
        closeStream = false;
        isDirectory = true;
    }

    public override long Crc => 0;

    public override string Key { get; }

    public override long CompressedSize => 0;

    public override long Size { get; }

    public override DateTime? LastModifiedTime { get; }

    public override DateTime? CreatedTime => null;

    public override DateTime? LastAccessedTime => null;

    public override DateTime? ArchivedTime => null;

    public override bool IsEncrypted => false;

    public override bool IsDirectory => isDirectory;

    public override bool IsSplitAfter => false;

    internal override IEnumerable<FilePart> Parts => throw new NotImplementedException();
    Stream IWritableArchiveEntry.Stream => stream ?? Stream.Null;

    public override Stream OpenEntryStream()
    {
        if (stream is null)
        {
            return Stream.Null;
        }
        //ensure new stream is at the start, this could be reset
        stream.Seek(0, SeekOrigin.Begin);
        return SharpCompressStream.Create(stream, leaveOpen: true);
    }

    internal override void Close()
    {
        if (closeStream && stream is not null)
        {
            stream.Dispose();
        }
    }
}
