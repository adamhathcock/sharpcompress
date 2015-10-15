namespace SharpCompress.Common.Tar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Tar.Headers;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class TarFilePart : FilePart
    {
        [CompilerGenerated]
        private TarHeader _Header_k__BackingField;
        private readonly Stream seekableStream;

        internal TarFilePart(TarHeader header, Stream seekableStream)
        {
            this.seekableStream = seekableStream;
            this.Header = header;
        }

        internal override Stream GetCompressedStream()
        {
            if (this.seekableStream != null)
            {
                this.seekableStream.Position = this.Header.DataStartPosition.Value;
                return new ReadOnlySubStream(this.seekableStream, this.Header.Size);
            }
            return this.Header.PackedStream;
        }

        internal override Stream GetRawStream()
        {
            return null;
        }

        internal override string FilePartName
        {
            get
            {
                return this.Header.Name;
            }
        }

        internal TarHeader Header
        {
            [CompilerGenerated]
            get
            {
                return this._Header_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Header_k__BackingField = value;
            }
        }
    }
}

