namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class SignHeader : RarHeader
    {
        [CompilerGenerated]
        private short <ArcNameSize>k__BackingField;
        [CompilerGenerated]
        private int <CreationTime>k__BackingField;
        [CompilerGenerated]
        private short <UserNameSize>k__BackingField;

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
                return this.<ArcNameSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<ArcNameSize>k__BackingField = value;
            }
        }

        internal int CreationTime
        {
            [CompilerGenerated]
            get
            {
                return this.<CreationTime>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<CreationTime>k__BackingField = value;
            }
        }

        internal short UserNameSize
        {
            [CompilerGenerated]
            get
            {
                return this.<UserNameSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<UserNameSize>k__BackingField = value;
            }
        }
    }
}

