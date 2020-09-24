using System;

namespace SharpCompress.Common
{
    public sealed class CompressedBytesReadEventArgs : EventArgs
    {
        public CompressedBytesReadEventArgs(long compressedBytesRead, long currentFilePartCompressedBytesRead)
        {
            CompressedBytesRead = compressedBytesRead;
            CurrentFilePartCompressedBytesRead = currentFilePartCompressedBytesRead;
        }

        /// <summary>
        /// Compressed bytes read for the current entry
        /// </summary>
        public long CompressedBytesRead { get; }

        /// <summary>
        /// Current file part read for Multipart files (e.g. Rar)
        /// </summary>
        public long CurrentFilePartCompressedBytesRead { get; }
    }
}