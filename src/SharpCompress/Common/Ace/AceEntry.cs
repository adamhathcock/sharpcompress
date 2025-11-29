using System;
using System.Collections.Generic;

namespace SharpCompress.Common.Ace;

/// <summary>
/// Represents an entry (file or directory) within an ACE archive.
/// </summary>
public class AceEntry : Entry
{
    private readonly AceFilePart? _filePart;

    internal AceEntry(AceFilePart? filePart)
    {
        _filePart = filePart;
    }

    public override long Crc
    {
        get
        {
            if (_filePart is null)
            {
                return 0;
            }
            return _filePart.Header.Crc32;
        }
    }

    public override string? Key => _filePart?.Header.Name;

    public override string? LinkTarget => null;

    public override long CompressedSize => _filePart?.Header.CompressedSize ?? 0;

    public override CompressionType CompressionType =>
        _filePart?.Header.CompressionMethod ?? CompressionType.Unknown;

    public override long Size => _filePart?.Header.OriginalSize ?? 0;

    public override DateTime? LastModifiedTime => _filePart?.Header.DateTime;

    public override DateTime? CreatedTime => null;

    public override DateTime? LastAccessedTime => null;

    public override DateTime? ArchivedTime => null;

    public override bool IsEncrypted => false;

    public override bool IsDirectory => _filePart?.Header.IsDirectory ?? false;

    public override bool IsSplitAfter => false;

    public override int? Attrib => _filePart?.Header.FileAttributes;

    internal override IEnumerable<FilePart> Parts => _filePart.Empty();
}
