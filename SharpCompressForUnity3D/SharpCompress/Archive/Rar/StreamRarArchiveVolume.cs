namespace SharpCompress.Archive.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class StreamRarArchiveVolume : RarVolume
    {
        internal StreamRarArchiveVolume(Stream stream, string password, Options options) : base(StreamingMode.Seekable, stream, password, options)
        {
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader)
        {
            return new SeekableFilePart(markHeader, fileHeader, base.Stream, base.Password);
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return base.GetVolumeFileParts();
        }
    }
}

