namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class ProtectHeader : RarHeader
    {
        [CompilerGenerated]
        private byte[] _Mark_k__BackingField;
        [CompilerGenerated]
        private ushort _RecSectors_k__BackingField;
        [CompilerGenerated]
        private uint _TotalBlocks_k__BackingField;
        [CompilerGenerated]
        private byte _Version_k__BackingField;

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
                return this._Mark_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Mark_k__BackingField = value;
            }
        }

        internal ushort RecSectors
        {
            [CompilerGenerated]
            get
            {
                return this._RecSectors_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._RecSectors_k__BackingField = value;
            }
        }

        internal uint TotalBlocks
        {
            [CompilerGenerated]
            get
            {
                return this._TotalBlocks_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._TotalBlocks_k__BackingField = value;
            }
        }

        internal byte Version
        {
            [CompilerGenerated]
            get
            {
                return this._Version_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Version_k__BackingField = value;
            }
        }
    }
}

