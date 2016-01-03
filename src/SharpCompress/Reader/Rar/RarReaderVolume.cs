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
        internal RarReaderVolume(Stream stream, string password, Options options)
            : base(StreamingMode.Streaming, stream, password, options)
        {
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, SignatureType signatureType)
        {
            return new NonSeekableStreamFilePart(signatureType, fileHeader);
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return GetVolumeFileParts();
        }
    }
}