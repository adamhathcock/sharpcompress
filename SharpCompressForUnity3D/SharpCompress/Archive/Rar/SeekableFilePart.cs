namespace SharpCompress.Archive.Rar
{
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.IO;

    internal class SeekableFilePart : RarFilePart
    {
        private readonly string password;
        private readonly Stream stream;

        internal SeekableFilePart(MarkHeader mh, FileHeader fh, Stream stream, string password) : base(mh, fh)
        {
            this.stream = stream;
            this.password = password;
        }

        internal override Stream GetCompressedStream()
        {
            this.stream.Position = base.FileHeader.DataStartPosition;
            if (base.FileHeader.Salt != null)
            {
                return new RarCryptoWrapper(this.stream, this.password, base.FileHeader.Salt);
            }
            return this.stream;
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

