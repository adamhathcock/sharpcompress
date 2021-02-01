using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Writers.GZip
{
    public sealed class GZipWriter : AbstractWriter
    {
        private bool _wroteToStream;

        public GZipWriter(Stream destination, GZipWriterOptions? options = null)
            : base(ArchiveType.GZip, options ?? new GZipWriterOptions())
        {
            if (WriterOptions.LeaveStreamOpen)
            {
                destination = new NonDisposingStream(destination);
            }
            InitializeStream(new GZipStream(destination, CompressionMode.Compress,
                                           options?.CompressionLevel ?? CompressionLevel.Default,
                                           WriterOptions.ArchiveEncoding.GetEncoding()));
        }

        protected override ValueTask DisposeAsyncCore()
        {
            //dispose here to finish the GZip, GZip won't close the underlying stream
            return OutputStream.DisposeAsync();
        }

        public override async Task WriteAsync(string filename, Stream source, DateTime? modificationTime, CancellationToken cancellationToken)
        {
            if (_wroteToStream)
            {
                throw new ArgumentException("Can only write a single stream to a GZip file.");
            }
            GZipStream stream = (GZipStream)OutputStream;
            stream.FileName = filename;
            stream.LastModified = modificationTime;
            await source.TransferToAsync(stream);
            _wroteToStream = true;
        }
    }
}