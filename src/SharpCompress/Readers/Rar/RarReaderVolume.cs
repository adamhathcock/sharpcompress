using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Readers.Rar
{
    public class RarReaderVolume : RarVolume
    {
        internal RarReaderVolume(Stream stream, ReaderOptions options)
            : base(StreamingMode.Streaming, stream, options)
        {
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader)
        {
            return new NonSeekableStreamFilePart(markHeader, fileHeader);
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return GetVolumeFileParts();
        }
    }
}