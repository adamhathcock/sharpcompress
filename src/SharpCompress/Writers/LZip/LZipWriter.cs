using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Writers.LZip
{
    public class LZipWriter : AbstractWriter
    {
        private bool wroteToStream;

        public LZipWriter(Stream destination, bool leaveOpen = false)
            : base(ArchiveType.GZip)
        {
            InitalizeStream(new LZipStream(destination, CompressionMode.Compress, leaveOpen), !leaveOpen);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                //dispose here to finish the GZip, GZip won't close the underlying stream
                OutputStream.Dispose();
            }
            base.Dispose(isDisposing);
        }

        public override void Write(string filename, Stream source, DateTime? modificationTime)
        {
            if (wroteToStream)
            {
                throw new ArgumentException("Can only write a single stream to a GZip file.");
            }
            LZipStream stream = OutputStream as LZipStream;
            source.TransferTo(stream);
            wroteToStream = true;
        }
    }
}