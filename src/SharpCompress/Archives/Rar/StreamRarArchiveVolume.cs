using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar;

internal class StreamRarArchiveVolume : RarVolume
{
    internal StreamRarArchiveVolume(Stream stream, ReaderOptions options, int index)
        : base(StreamingMode.Seekable, stream, options, index) { }

    internal override IEnumerable<RarFilePart> ReadFileParts() => GetVolumeFileParts();

    internal override RarFilePart CreateFilePart(MarkHeader markHeader, FileHeader fileHeader) =>
        new SeekableFilePart(markHeader, fileHeader, Index, Stream, ReaderOptions.Password);
}
