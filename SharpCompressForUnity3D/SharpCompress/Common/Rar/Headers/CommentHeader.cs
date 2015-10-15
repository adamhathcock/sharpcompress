namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class CommentHeader : RarHeader
    {
        [CompilerGenerated]
        private short _CommCRC_k__BackingField;
        [CompilerGenerated]
        private byte _UnpMethod_k__BackingField;
        [CompilerGenerated]
        private short _UnpSize_k__BackingField;
        [CompilerGenerated]
        private byte _UnpVersion_k__BackingField;

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
                return this._CommCRC_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._CommCRC_k__BackingField = value;
            }
        }

        internal byte UnpMethod
        {
            [CompilerGenerated]
            get
            {
                return this._UnpMethod_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._UnpMethod_k__BackingField = value;
            }
        }

        internal short UnpSize
        {
            [CompilerGenerated]
            get
            {
                return this._UnpSize_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._UnpSize_k__BackingField = value;
            }
        }

        internal byte UnpVersion
        {
            [CompilerGenerated]
            get
            {
                return this._UnpVersion_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._UnpVersion_k__BackingField = value;
            }
        }
    }
}

