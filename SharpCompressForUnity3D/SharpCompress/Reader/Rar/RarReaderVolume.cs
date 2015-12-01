﻿namespace SharpCompress.Reader.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using SharpCompress.IO;

    public class RarReaderVolume : RarVolume
    {
        internal RarReaderVolume(Stream stream, string password, Options options) : base(StreamingMode.Streaming, stream, password, options)
        {
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader)
        {
            return new NonSeekableStreamFilePart(markHeader, fileHeader);
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return base.GetVolumeFileParts();
        }
    }
}

