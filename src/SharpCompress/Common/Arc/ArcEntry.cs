using System;
using System.Collections.Generic;

namespace SharpCompress.Common.Arc;

public class ArcEntry : Entry
{
  private readonly ArcFilePart? _filePart;

  internal ArcEntry(ArcFilePart? filePart)
  {
    _filePart = filePart;
  }

  public override long Crc
  {
    get
    {
      if (_filePart == null)
      {
        return 0;
      }
      return _filePart.Header.Crc16;
    }
  }

  public override string? Key => _filePart?.Header.Name;

  public override string? LinkTarget => null;

  public override long CompressedSize => _filePart?.Header.CompressedSize ?? 0;

  public override CompressionType CompressionType =>
    _filePart?.Header.CompressionMethod ?? CompressionType.Unknown;

#pragma warning disable CA1065
  public override long Size => throw new NotImplementedException();
#pragma warning restore CA1065

  public override DateTime? LastModifiedTime => null;

  public override DateTime? CreatedTime => null;

  public override DateTime? LastAccessedTime => null;

  public override DateTime? ArchivedTime => null;

  public override bool IsEncrypted => false;

  public override bool IsDirectory => false;

  public override bool IsSplitAfter => false;

  internal override IEnumerable<FilePart> Parts => _filePart.Empty();
}
