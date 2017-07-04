using System.IO;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common
{
    public abstract class Volume : IVolume
    {
        private readonly Stream actualStream;

        internal Volume(Stream stream, ReaderOptions readerOptions)
        {
            actualStream = stream;
            ReaderOptions = readerOptions;
        }

        internal Stream Stream => new NonDisposingStream(actualStream);

        protected ReaderOptions ReaderOptions { get; }

        /// <summary>
        /// RarArchive is the first volume of a multi-part archive.
        /// Only Rar 3.0 format and higher
        /// </summary>
        public virtual bool IsFirstVolume => true;

        /// <summary>
        /// RarArchive is part of a multi-part archive.
        /// </summary>
        public virtual bool IsMultiVolume => true;

        private bool disposed;

        public void Dispose()
        {
            if (!ReaderOptions.LeaveStreamOpen && !disposed)
            {
                actualStream.Dispose();
                disposed = true;
            }
        }
    }
}