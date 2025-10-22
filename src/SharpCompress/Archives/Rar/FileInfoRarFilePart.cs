using System.IO;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Archives.Rar;

internal sealed class FileInfoRarFilePart : SeekableFilePart
{
    internal FileInfoRarFilePart(
        FileInfoRarArchiveVolume volume,
        string? password,
        MarkHeader mh,
        FileHeader fh,
        FileInfo fi
    )
        : base(mh, fh, volume.Index, volume.Stream, password)
    {
        FileInfo = fi;
    }

    internal FileInfo FileInfo { get; }

    internal override string FilePartName =>
        "Rar File: " + FileInfo.FullName + " File Entry: " + FileHeader.FileName;
}
