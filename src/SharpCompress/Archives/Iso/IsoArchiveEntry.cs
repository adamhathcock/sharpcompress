using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives.Iso;

public class IsoArchiveEntry : IArchiveEntry
{
    private readonly IsoArchive archive;
    private readonly IsoFilePart part;

    internal IsoArchiveEntry(IsoArchive archive, IsoFilePart part)
    {
        this.archive = archive;
        this.part = part;
    }

    public bool IsComplete => true;

    public IArchive Archive => archive;

    public string Key => part.Key;

    public long CompressedSize => part.CompressedSize;

    public long Size => part.Size;

    public DateTime? LastModifiedTime => part.LastModifiedTime;

    public DateTime? CreatedTime => part.CreatedTime;

    public DateTime? LastAccessedTime => part.LastAccessedTime;

    public DateTime? ArchivedTime => part.ArchivedTime;

    public bool IsEncrypted => false;

    public bool IsDirectory => part.IsDirectory;

    public bool IsSplitAfter => false;

    public Stream OpenEntryStream()
    {
        var stream = part.GetStream();
        stream.Seek(part.EntryStartPosition, SeekOrigin.Begin);
        return stream;
    }

    internal void Close()
    {
        part.Close();
    }
}
