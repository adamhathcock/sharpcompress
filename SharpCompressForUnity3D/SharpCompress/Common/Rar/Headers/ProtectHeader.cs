namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class ProtectHeader : RarHeader
    {
        [CompilerGenerated]
        private byte[] <Mark>k__BackingField;
        [CompilerGenerated]
        private ushort <RecSectors>k__BackingField;
        [CompilerGenerated]
        private uint <TotalBlocks>k__BackingField;
        [CompilerGenerated]
        private byte <Version>k__BackingField;

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            this.Version = reader.ReadByte();
            this.RecSectors = reader.ReadUInt16();
            this.TotalBlocks = reader.ReadUInt32();
            this.Mark = reader.ReadBytes(8);
        }

        internal uint DataSize
        {
            get
            {
                return base.AdditionalSize;
            }
        }

        internal byte[] Mark
        {
            [CompilerGenerated]
            get
            {
                return this.<Mark>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Mark>k__BackingField = value;
            }
        }

        internal ushort RecSectors
        {
            [CompilerGenerated]
            get
            {
                return this.<RecSectors>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<RecSectors>k__BackingField = value;
            }
        }

        internal uint TotalBlocks
        {
            [CompilerGenerated]
            get
            {
                return this.<TotalBlocks>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<TotalBlocks>k__BackingField = value;
            }
        }

        internal byte Version
        {
            [CompilerGenerated]
            get
            {
                return this.<Version>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Version>k__BackingField = value;
            }
        }
    }
}

