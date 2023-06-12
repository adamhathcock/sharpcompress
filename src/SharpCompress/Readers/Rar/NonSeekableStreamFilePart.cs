using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Readers.Rar;

internal class NonSeekableStreamFilePart : RarFilePart
{
    internal NonSeekableStreamFilePart(MarkHeader mh, FileHeader fh, int index = 0)
        : base(mh, fh, index) { }

    internal override Stream GetCompressedStream() => FileHeader.PackedStream;

    internal override Stream? GetRawStream() => FileHeader.PackedStream;

    internal override string FilePartName => "Unknown Stream - File Entry: " + FileHeader.FileName;
}
