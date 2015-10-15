namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class DirectoryEndHeader : ZipHeader
    {
        [CompilerGenerated]
        private byte[] <Comment>k__BackingField;
        [CompilerGenerated]
        private ushort <CommentLength>k__BackingField;
        [CompilerGenerated]
        private uint <DirectorySize>k__BackingField;
        [CompilerGenerated]
        private uint <DirectoryStartOffsetRelativeToDisk>k__BackingField;
        [CompilerGenerated]
        private ushort <FirstVolumeWithDirectory>k__BackingField;
        [CompilerGenerated]
        private ushort <TotalNumberOfEntries>k__BackingField;
        [CompilerGenerated]
        private ushort <TotalNumberOfEntriesInDisk>k__BackingField;
        [CompilerGenerated]
        private ushort <VolumeNumber>k__BackingField;

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
                return this.<Comment>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Comment>k__BackingField = value;
            }
        }

        public ushort CommentLength
        {
            [CompilerGenerated]
            get
            {
                return this.<CommentLength>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<CommentLength>k__BackingField = value;
            }
        }

        public uint DirectorySize
        {
            [CompilerGenerated]
            get
            {
                return this.<DirectorySize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<DirectorySize>k__BackingField = value;
            }
        }

        public uint DirectoryStartOffsetRelativeToDisk
        {
            [CompilerGenerated]
            get
            {
                return this.<DirectoryStartOffsetRelativeToDisk>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<DirectoryStartOffsetRelativeToDisk>k__BackingField = value;
            }
        }

        public ushort FirstVolumeWithDirectory
        {
            [CompilerGenerated]
            get
            {
                return this.<FirstVolumeWithDirectory>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FirstVolumeWithDirectory>k__BackingField = value;
            }
        }

        public ushort TotalNumberOfEntries
        {
            [CompilerGenerated]
            get
            {
                return this.<TotalNumberOfEntries>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<TotalNumberOfEntries>k__BackingField = value;
            }
        }

        public ushort TotalNumberOfEntriesInDisk
        {
            [CompilerGenerated]
            get
            {
                return this.<TotalNumberOfEntriesInDisk>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<TotalNumberOfEntriesInDisk>k__BackingField = value;
            }
        }

        public ushort VolumeNumber
        {
            [CompilerGenerated]
            get
            {
                return this.<VolumeNumber>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<VolumeNumber>k__BackingField = value;
            }
        }
    }
}

