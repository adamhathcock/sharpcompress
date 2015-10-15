namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class SignHeader : RarHeader
    {
        [CompilerGenerated]
        private short _ArcNameSize_k__BackingField;
        [CompilerGenerated]
        private int _CreationTime_k__BackingField;
        [CompilerGenerated]
        private short _UserNameSize_k__BackingField;

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            this.CreationTime = reader.ReadInt32();
            this.ArcNameSize = reader.ReadInt16();
            this.UserNameSize = reader.ReadInt16();
        }

        internal short ArcNameSize
        {
            [CompilerGenerated]
            get
            {
                return this._ArcNameSize_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._ArcNameSize_k__BackingField = value;
            }
        }

        internal int CreationTime
        {
            [CompilerGenerated]
            get
            {
                return this._CreationTime_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._CreationTime_k__BackingField = value;
            }
        }

        internal short UserNameSize
        {
            [CompilerGenerated]
            get
            {
                return this._UserNameSize_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._UserNameSize_k__BackingField = value;
            }
        }
    }
}

