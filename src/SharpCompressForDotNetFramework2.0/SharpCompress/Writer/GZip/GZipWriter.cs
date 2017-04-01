using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressor;
using SharpCompress.Compressor.Deflate;

namespace SharpCompress.Writer.GZip
{
    public class GZipWriter : AbstractWriter
    {
        private bool wroteToStream;

        public GZipWriter(Stream destination)
            : base(ArchiveType.GZip)
        {
            InitalizeStream(new GZipStream(destination, CompressionMode.Compress, true), true);
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
            GZipStream stream = OutputStream as GZipStream;
            stream.FileName = filename;
            stream.LastModified = modificationTime;
            source.TransferTo(stream);
            wroteToStream = true;
        }
    }
}
