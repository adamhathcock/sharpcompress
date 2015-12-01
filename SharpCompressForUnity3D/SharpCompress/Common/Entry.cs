namespace SharpCompress.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public abstract class Entry : IEntry
    {
        [CompilerGenerated]
        private bool _IsSolid_k__BackingField;

        protected Entry()
        {
        }

        internal virtual void Close()
        {
        }

        public abstract DateTime? ArchivedTime { get; }

        public virtual int? Attrib
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public abstract long CompressedSize { get; }

        public abstract SharpCompress.Common.CompressionType CompressionType { get; }

        public abstract long Crc { get; }

        public abstract DateTime? CreatedTime { get; }

        public abstract bool IsDirectory { get; }

        public abstract bool IsEncrypted { get; }

        internal bool IsSolid
        {
            [CompilerGenerated]
            get
            {
                return this._IsSolid_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._IsSolid_k__BackingField = value;
            }
        }

        public abstract bool IsSplit { get; }

        public abstract string Key { get; }

        public abstract DateTime? LastAccessedTime { get; }

        public abstract DateTime? LastModifiedTime { get; }

        internal abstract IEnumerable<FilePart> Parts { get; }

        public abstract long Size { get; }
    }
}

