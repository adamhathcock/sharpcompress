using System;

namespace SharpCompress.Common
{
    public sealed class FilePartExtractionBeginEventArgs : EventArgs
    {
        public FilePartExtractionBeginEventArgs(string name, long size, long compressedSize)
        {
            Name = name;
            Size = size;
            CompressedSize = compressedSize;
        }

        /// <summary>
        /// File name for the part for the current entry
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Uncompressed size of the current entry in the part
        /// </summary>
        public long Size { get; }

        /// <summary>
        /// Compressed size of the current entry in the part
        /// </summary>
        public long CompressedSize { get; }
    }
}