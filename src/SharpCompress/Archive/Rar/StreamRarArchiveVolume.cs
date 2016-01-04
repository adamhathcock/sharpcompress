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
        internal StreamRarArchiveVolume(Stream stream, string password, Options options)
            : base(StreamingMode.Seekable, stream, password, options)
        {
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return GetVolumeFileParts();
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, SignatureType signatureType)
        {
            return new SeekableFilePart(signatureType, fileHeader, Stream, Password);
        }
    }
}