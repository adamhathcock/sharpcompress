using System.IO;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common
{
    public abstract class Volume : IVolume
    {
        private readonly Stream _actualStream;

        internal Volume(Stream stream, ReaderOptions readerOptions)
        {
            ReaderOptions = readerOptions;
            if (readerOptions.LeaveStreamOpen)
            {
                stream = new NonDisposingStream(stream);
            }
            _actualStream = stream;
        }

        internal Stream Stream => _actualStream;

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

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _actualStream.Dispose();
                _disposed = true;
            }
        }
    }
}