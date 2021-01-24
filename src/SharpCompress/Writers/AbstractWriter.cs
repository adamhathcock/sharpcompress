#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers
{
    public abstract class AbstractWriter : IWriter
    {
        protected AbstractWriter(ArchiveType type, WriterOptions writerOptions)
        {
            WriterType = type;
            WriterOptions = writerOptions;
        }

        protected void InitializeStream(Stream stream)
        {
            OutputStream = stream;
        }

        protected Stream OutputStream { get; private set; }

        public ArchiveType WriterType { get; }

        protected WriterOptions WriterOptions { get; }

        public abstract Task WriteAsync(string filename, Stream source, DateTime? modificationTime, CancellationToken cancellationToken);

        public virtual ValueTask DisposeAsync()
        {
            return OutputStream.DisposeAsync();
        }
    }
}