using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Readers.Rar;

public class RarReaderVolume : RarVolume
{
    internal RarReaderVolume(Stream stream, ReaderOptions options, int index)
        : base(StreamingMode.Streaming, stream, options, index) { }

    internal override RarFilePart CreateFilePart(MarkHeader markHeader, FileHeader fileHeader) =>
        new NonSeekableStreamFilePart(markHeader, fileHeader, Index);

    internal override IEnumerable<RarFilePart> ReadFileParts() => GetVolumeFileParts();
}
