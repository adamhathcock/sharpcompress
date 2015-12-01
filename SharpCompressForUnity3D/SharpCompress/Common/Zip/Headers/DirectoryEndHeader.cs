namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class DirectoryEndHeader : ZipHeader
    {
        [CompilerGenerated]
        private byte[] _Comment_k__BackingField;
        [CompilerGenerated]
        private ushort _CommentLength_k__BackingField;
        [CompilerGenerated]
        private uint _DirectorySize_k__BackingField;
        [CompilerGenerated]
        private uint _DirectoryStartOffsetRelativeToDisk_k__BackingField;
        [CompilerGenerated]
        private ushort _FirstVolumeWithDirectory_k__BackingField;
        [CompilerGenerated]
        private ushort _TotalNumberOfEntries_k__BackingField;
        [CompilerGenerated]
        private ushort _TotalNumberOfEntriesInDisk_k__BackingField;
        [CompilerGenerated]
        private ushort _VolumeNumber_k__BackingField;

        public DirectoryEndHeader() : base(ZipHeaderType.DirectoryEnd)
        {
        }

        internal override void Read(BinaryReader reader)
        {
            this.VolumeNumber = reader.ReadUInt16();
            this.FirstVolumeWithDirectory = reader.ReadUInt16();
            this.TotalNumberOfEntriesInDisk = reader.ReadUInt16();
            this.TotalNumberOfEntries = reader.ReadUInt16();
            this.DirectorySize = reader.ReadUInt32();
            this.DirectoryStartOffsetRelativeToDisk = reader.ReadUInt32();
            this.CommentLength = reader.ReadUInt16();
            this.Comment = reader.ReadBytes(this.CommentLength);
        }

        internal override void Write(BinaryWriter writer)
        {
            writer.Write(this.VolumeNumber);
            writer.Write(this.FirstVolumeWithDirectory);
            writer.Write(this.TotalNumberOfEntriesInDisk);
            writer.Write(this.TotalNumberOfEntries);
            writer.Write(this.DirectorySize);
            writer.Write(this.DirectoryStartOffsetRelativeToDisk);
            writer.Write(this.CommentLength);
            writer.Write(this.Comment);
        }

        public byte[] Comment
        {
            [CompilerGenerated]
            get
            {
                return this._Comment_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Comment_k__BackingField = value;
            }
        }

        public ushort CommentLength
        {
            [CompilerGenerated]
            get
            {
                return this._CommentLength_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._CommentLength_k__BackingField = value;
            }
        }

        public uint DirectorySize
        {
            [CompilerGenerated]
            get
            {
                return this._DirectorySize_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._DirectorySize_k__BackingField = value;
            }
        }

        public uint DirectoryStartOffsetRelativeToDisk
        {
            [CompilerGenerated]
            get
            {
                return this._DirectoryStartOffsetRelativeToDisk_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._DirectoryStartOffsetRelativeToDisk_k__BackingField = value;
            }
        }

        public ushort FirstVolumeWithDirectory
        {
            [CompilerGenerated]
            get
            {
                return this._FirstVolumeWithDirectory_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._FirstVolumeWithDirectory_k__BackingField = value;
            }
        }

        public ushort TotalNumberOfEntries
        {
            [CompilerGenerated]
            get
            {
                return this._TotalNumberOfEntries_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._TotalNumberOfEntries_k__BackingField = value;
            }
        }

        public ushort TotalNumberOfEntriesInDisk
        {
            [CompilerGenerated]
            get
            {
                return this._TotalNumberOfEntriesInDisk_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._TotalNumberOfEntriesInDisk_k__BackingField = value;
            }
        }

        public ushort VolumeNumber
        {
            [CompilerGenerated]
            get
            {
                return this._VolumeNumber_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._VolumeNumber_k__BackingField = value;
            }
        }
    }
}

