namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class CommentHeader : RarHeader
    {
        [CompilerGenerated]
        private short <CommCRC>k__BackingField;
        [CompilerGenerated]
        private byte <UnpMethod>k__BackingField;
        [CompilerGenerated]
        private short <UnpSize>k__BackingField;
        [CompilerGenerated]
        private byte <UnpVersion>k__BackingField;

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            this.UnpSize = reader.ReadInt16();
            this.UnpVersion = reader.ReadByte();
            this.UnpMethod = reader.ReadByte();
            this.CommCRC = reader.ReadInt16();
        }

        internal short CommCRC
        {
            [CompilerGenerated]
            get
            {
                return this.<CommCRC>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<CommCRC>k__BackingField = value;
            }
        }

        internal byte UnpMethod
        {
            [CompilerGenerated]
            get
            {
                return this.<UnpMethod>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<UnpMethod>k__BackingField = value;
            }
        }

        internal short UnpSize
        {
            [CompilerGenerated]
            get
            {
                return this.<UnpSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<UnpSize>k__BackingField = value;
            }
        }

        internal byte UnpVersion
        {
            [CompilerGenerated]
            get
            {
                return this.<UnpVersion>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<UnpVersion>k__BackingField = value;
            }
        }
    }
}

