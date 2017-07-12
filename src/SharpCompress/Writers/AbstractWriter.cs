using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Writers
{
    public abstract class AbstractWriter : IWriter
    {
        private bool isDisposed;

        protected AbstractWriter(ArchiveType type, WriterOptions writerOptions)
        {
            WriterType = type;
            WriterOptions = writerOptions;
        }

        protected void InitalizeStream(Stream stream)
        {
            OutputStream = stream;
        }

        protected Stream OutputStream { get; private set; }

        public ArchiveType WriterType { get; }

        protected WriterOptions WriterOptions { get; }

        public abstract void Write(string filename, Stream source, DateTime? modificationTime);

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing && !WriterOptions.LeaveStreamOpen)
            {
                OutputStream.Dispose();
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                GC.SuppressFinalize(this);
                Dispose(true);
                isDisposed = true;
            }
        }

        ~AbstractWriter()
        {
            if (!isDisposed)
            {
                Dispose(false);
                isDisposed = true;
            }
        }
    }
}