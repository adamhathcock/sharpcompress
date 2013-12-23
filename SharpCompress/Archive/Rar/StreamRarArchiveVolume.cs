using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Archive.Rar
{
    internal class StreamRarArchiveVolume : RarVolume
    {
        internal StreamRarArchiveVolume(Stream stream, Options options)
            : base(StreamingMode.Seekable, stream, options)
        {
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return GetVolumeFileParts();
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader)
        {
            return new StreamFilePart(markHeader, fileHeader, Stream);
        }
    }
}