﻿using System;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Common.Rar
{
    public abstract class RarEntry : Entry
    {
        internal abstract FileHeader FileHeader { get; }

        /// <summary>
        /// The File's 32 bit CRC Hash
        /// </summary>
        public override long Crc => FileHeader.FileCRC;

        /// <summary>
        /// The path of the file internal to the Rar Archive.
        /// </summary>
        public override string Key => FileHeader.FileName;

        /// <summary>
        /// The entry last modified time in the archive, if recorded
        /// </summary>
        public override DateTime? LastModifiedTime => FileHeader.FileLastModifiedTime;

        /// <summary>
        /// The entry create time in the archive, if recorded
        /// </summary>
        public override DateTime? CreatedTime => FileHeader.FileCreatedTime;

        /// <summary>
        /// The entry last accessed time in the archive, if recorded
        /// </summary>
        public override DateTime? LastAccessedTime => FileHeader.FileLastAccessedTime;

        /// <summary>
        /// The entry time whend archived, if recorded
        /// </summary>
        public override DateTime? ArchivedTime => FileHeader.FileArchivedTime;

        /// <summary>
        /// Entry is password protected and encrypted and cannot be extracted.
        /// </summary>
        public override bool IsEncrypted => FileHeader.FileFlags.HasFlag(FileFlags.PASSWORD);

        /// <summary>
        /// Entry is password protected and encrypted and cannot be extracted.
        /// </summary>
        public override bool IsDirectory => FileHeader.FileFlags.HasFlag(FileFlags.DIRECTORY);

        public override bool IsSplit => FileHeader.FileFlags.HasFlag(FileFlags.SPLIT_AFTER);

        public override string ToString()
        {
            return string.Format("Entry Path: {0} Compressed Size: {1} Uncompressed Size: {2} CRC: {3}",
                                 Key, CompressedSize, Size, Crc);
        }
    }
}