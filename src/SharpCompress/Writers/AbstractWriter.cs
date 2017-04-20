using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Writers
{
    public abstract class AbstractWriter : IWriter
    {
        private bool closeStream;
        private bool isDisposed;

        protected AbstractWriter(ArchiveType type)
        {
            WriterType = type;
        }

        protected void InitalizeStream(Stream stream, bool closeStream)
        {
            OutputStream = stream;
            this.closeStream = closeStream;
        }

        protected Stream OutputStream { get; private set; }

        public ArchiveType WriterType { get; }

        public abstract void Write(string filename, Stream source, DateTime? modificationTime, Action<long, int> partTransferredAction = null);

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing && closeStream)
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