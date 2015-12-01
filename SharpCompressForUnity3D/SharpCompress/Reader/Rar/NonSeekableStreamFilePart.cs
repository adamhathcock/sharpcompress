namespace SharpCompress.Reader.Rar
{
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.IO;

    internal class NonSeekableStreamFilePart : RarFilePart
    {
        internal NonSeekableStreamFilePart(MarkHeader mh, FileHeader fh) : base(mh, fh)
        {
        }

        internal override Stream GetCompressedStream()
        {
            return base.FileHeader.PackedStream;
        }

        internal override string FilePartName
        {
            get
            {
                return ("Unknown Stream - File Entry: " + base.FileHeader.FileName);
            }
        }
    }
}

