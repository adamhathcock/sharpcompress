namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class AVHeader : RarHeader
    {
        [CompilerGenerated]
        private int <AVInfoCRC>k__BackingField;
        [CompilerGenerated]
        private byte <AVVersion>k__BackingField;
        [CompilerGenerated]
        private byte <Method>k__BackingField;
        [CompilerGenerated]
        private byte <UnpackVersion>k__BackingField;

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            this.UnpackVersion = reader.ReadByte();
            this.Method = reader.ReadByte();
            this.AVVersion = reader.ReadByte();
            this.AVInfoCRC = reader.ReadInt32();
        }

        internal int AVInfoCRC
        {
            [CompilerGenerated]
            get
            {
                return this.<AVInfoCRC>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<AVInfoCRC>k__BackingField = value;
            }
        }

        internal byte AVVersion
        {
            [CompilerGenerated]
            get
            {
                return this.<AVVersion>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<AVVersion>k__BackingField = value;
            }
        }

        internal byte Method
        {
            [CompilerGenerated]
            get
            {
                return this.<Method>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Method>k__BackingField = value;
            }
        }

        internal byte UnpackVersion
        {
            [CompilerGenerated]
            get
            {
                return this.<UnpackVersion>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<UnpackVersion>k__BackingField = value;
            }
        }
    }
}

