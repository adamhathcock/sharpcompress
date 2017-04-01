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

#if !PORTABLE
        public override FileInfo VolumeFile
        {
            get { return null; }
        }
#endif

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return GetVolumeFileParts();
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader)
        {
            return new SeekableStreamFilePart(markHeader, fileHeader, Stream);
        }
    }
}
