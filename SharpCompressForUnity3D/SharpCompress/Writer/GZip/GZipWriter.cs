namespace SharpCompress.Writer.GZip
{
    using SharpCompress;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.Writer;
    using System;
    using System.IO;
    using SharpCompress.Common;

    public class GZipWriter : AbstractWriter
    {
        private bool wroteToStream;

        public GZipWriter(Stream destination) : base(ArchiveType.GZip)
        {
            base.InitalizeStream(new GZipStream(destination, CompressionMode.Compress, true), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                base.OutputStream.Dispose();
            }
            base.Dispose(isDisposing);
        }

        public override void Write(string filename, Stream source, DateTime? modificationTime)
        {
            if (this.wroteToStream)
            {
                throw new ArgumentException("Can only write a single stream to a GZip file.");
            }
            GZipStream outputStream = base.OutputStream as GZipStream;
            outputStream.FileName = filename;
            outputStream.LastModified = modificationTime;
            Utility.TransferTo(source, outputStream);
            this.wroteToStream = true;
        }
    }
}

