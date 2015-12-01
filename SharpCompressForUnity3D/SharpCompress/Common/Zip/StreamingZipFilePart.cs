namespace SharpCompress.Common.Zip
{
    using SharpCompress;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.IO;
    using System;
    using System.IO;

    internal class StreamingZipFilePart : ZipFilePart
    {
        private Stream decompressionStream;

        internal StreamingZipFilePart(ZipFileEntry header, Stream stream) : base(header, stream)
        {
        }

        protected override Stream CreateBaseStream()
        {
            return base.Header.PackedStream;
        }

        internal BinaryReader FixStreamedFileLocation(ref RewindableStream rewindableStream)
        {
            if (base.Header.IsDirectory)
            {
                return new BinaryReader(rewindableStream);
            }
            if (base.Header.HasData)
            {
                if (this.decompressionStream == null)
                {
                    this.decompressionStream = this.GetCompressedStream();
                }
                Utility.SkipAll(this.decompressionStream);
                DeflateStream decompressionStream = this.decompressionStream as DeflateStream;
                if (decompressionStream != null)
                {
                    rewindableStream.Rewind(decompressionStream.InputBuffer);
                }
            }
            BinaryReader reader = new BinaryReader(rewindableStream);
            this.decompressionStream = null;
            return reader;
        }

        internal override Stream GetCompressedStream()
        {
            if (!base.Header.HasData)
            {
                return Stream.Null;
            }
            this.decompressionStream = base.CreateDecompressionStream(base.GetCryptoStream(this.CreateBaseStream()));
            if (base.LeaveStreamOpen)
            {
                return new NonDisposingStream(this.decompressionStream);
            }
            return this.decompressionStream;
        }
    }
}

