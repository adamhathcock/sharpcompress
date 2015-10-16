namespace SharpCompress.Common.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar.Headers;
    using System;

    public abstract class RarEntry : Entry
    {
        protected RarEntry()
        {
        }

        private bool fileFlags_HasFlag(FileFlags fileFlags1, FileFlags fileFlags2)
        {
            return (((fileFlags1 & fileFlags2)) == fileFlags2);
        }

        private bool FileFlags_HasFlag(FileFlags fileFlags1, FileFlags fileFlags2)
        {
            return (((fileFlags1 & fileFlags2)) == fileFlags2);
        }

        public override string ToString()
        {
            return string.Format("Entry Path: {0} Compressed Size: {1} Uncompressed Size: {2} CRC: {3}", new object[] { this.Key, this.CompressedSize, this.Size, this.Crc });
        }

        public override DateTime? ArchivedTime
        {
            get
            {
                return this.FileHeader.FileArchivedTime;
            }
        }

        public override long Crc
        {
            get
            {
                return (long) this.FileHeader.FileCRC;
            }
        }

        public override DateTime? CreatedTime
        {
            get
            {
                return this.FileHeader.FileCreatedTime;
            }
        }

        internal abstract SharpCompress.Common.Rar.Headers.FileHeader FileHeader { get; }

        public override bool IsDirectory
        {
            get
            {
                return this.FileFlags_HasFlag(this.FileHeader.FileFlags, FileFlags.DIRECTORY);
            }
        }

        public override bool IsEncrypted
        {
            get
            {
                return this.fileFlags_HasFlag(this.FileHeader.FileFlags, FileFlags.PASSWORD);
            }
        }

        public override bool IsSplit
        {
            get
            {
                return this.FileFlags_HasFlag(this.FileHeader.FileFlags, FileFlags.SPLIT_AFTER);
            }
        }

        public override string Key
        {
            get
            {
                return this.FileHeader.FileName;
            }
        }

        public override DateTime? LastAccessedTime
        {
            get
            {
                return this.FileHeader.FileLastAccessedTime;
            }
        }

        public override DateTime? LastModifiedTime
        {
            get
            {
                return this.FileHeader.FileLastModifiedTime;
            }
        }
    }
}

