using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.GZip
{
    public class GZipWriter : AbstractWriter
    {
        private bool wroteToStream;

        public GZipWriter(Stream destination, bool leaveOpen = false)
            : base(ArchiveType.GZip)
        {
            InitalizeStream(new GZipStream(destination, CompressionMode.Compress, leaveOpen), !leaveOpen);
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

        public override void Write(string filename, Stream source, DateTime? modificationTime, Action<long, int> partTransferredAction = null)
        {
            if (wroteToStream)
            {
                throw new ArgumentException("Can only write a single stream to a GZip file.");
            }
            GZipStream stream = OutputStream as GZipStream;
            stream.FileName = filename;
            stream.LastModified = modificationTime;
            source.TransferTo(stream, partTransferredAction);
            wroteToStream = true;
        }
    }
}