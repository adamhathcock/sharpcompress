namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class AVHeader : RarHeader
    {
        [CompilerGenerated]
        private int _AVInfoCRC_k__BackingField;
        [CompilerGenerated]
        private byte _AVVersion_k__BackingField;
        [CompilerGenerated]
        private byte _Method_k__BackingField;
        [CompilerGenerated]
        private byte _UnpackVersion_k__BackingField;

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
                return this._AVInfoCRC_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._AVInfoCRC_k__BackingField = value;
            }
        }

        internal byte AVVersion
        {
            [CompilerGenerated]
            get
            {
                return this._AVVersion_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._AVVersion_k__BackingField = value;
            }
        }

        internal byte Method
        {
            [CompilerGenerated]
            get
            {
                return this._Method_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Method_k__BackingField = value;
            }
        }

        internal byte UnpackVersion
        {
            [CompilerGenerated]
            get
            {
                return this._UnpackVersion_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._UnpackVersion_k__BackingField = value;
            }
        }
    }
}

