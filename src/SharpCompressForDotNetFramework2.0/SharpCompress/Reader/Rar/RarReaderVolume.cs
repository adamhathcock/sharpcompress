using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Reader.Rar
{
    public class RarReaderVolume : RarVolume
    {
        internal RarReaderVolume(Stream stream, Options options)
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


#if !PORTABLE
        public override FileInfo VolumeFile
        {
            get
            {
                return null;
            }
        }
#endif
    }
}
