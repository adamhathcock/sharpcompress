using System;
using System.Collections.Generic;

namespace SharpCompress.Common
{
    public abstract class Entry : SharpCompress.Common.IEntry
    {
        /// <summary>
        /// The File's 32 bit CRC Hash
        /// </summary>
        public abstract uint Crc
        {
            get;
        }

        /// <summary>
        /// The path of the file internal to the Rar Archive.
        /// </summary>
        public abstract string FilePath
        {
            get;
        }

        /// <summary>
        /// The compressed file size
        /// </summary>
        public abstract long CompressedSize
        {
            get;
        }

        /// <summary>
        /// The compression type
        /// </summary>
        public abstract CompressionType CompressionType
        {
            get;
        }

        /// <summary>
        /// The uncompressed file size
        /// </summary>
        public abstract long Size
        {
            get;
        }

        /// <summary>
        /// The entry last modified time in the archive, if recorded
        /// </summary>
        public abstract DateTime? LastModifiedTime
        {
            get;
        }

        /// <summary>
        /// The entry create time in the archive, if recorded
        /// </summary>
        public abstract DateTime? CreatedTime
        {
            get;
        }

        /// <summary>
        /// The entry last accessed time in the archive, if recorded
        /// </summary>
        public abstract DateTime? LastAccessedTime
        {
            get;
        }

        /// <summary>
        /// The entry time whend archived, if recorded
        /// </summary>
        public abstract DateTime? ArchivedTime
        {
            get;
        }

        /// <summary>
        /// Entry is password protected and encrypted and cannot be extracted.
        /// </summary>
        public abstract bool IsEncrypted
        {
            get;
        }

        /// <summary>
        /// Entry is password protected and encrypted and cannot be extracted.
        /// </summary>
        public abstract bool IsDirectory
        {
            get;
        }

        public abstract bool IsSplit
        {
            get;
        }

        internal abstract IEnumerable<FilePart> Parts
        {
            get;
        }

        internal abstract void Close();
    }
}