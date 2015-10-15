namespace SharpCompress.Compressor.Rar.decode
{
    using System;
    using System.Runtime.CompilerServices;

    internal class Decode
    {
        [CompilerGenerated]
        private int[] <DecodeLen>k__BackingField;
        [CompilerGenerated]
        private int[] <DecodeNum>k__BackingField;
        [CompilerGenerated]
        private int[] <DecodePos>k__BackingField;
        [CompilerGenerated]
        private int <MaxNum>k__BackingField;

        internal Decode() : this(new int[2])
        {
        }

        protected Decode(int[] customDecodeNum)
        {
            this.DecodeLen = new int[0x10];
            this.DecodePos = new int[0x10];
            this.DecodeNum = customDecodeNum;
        }

        internal int[] DecodeLen
        {
            [CompilerGenerated]
            get
            {
                return this.<DecodeLen>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<DecodeLen>k__BackingField = value;
            }
        }

        internal int[] DecodeNum
        {
            [CompilerGenerated]
            get
            {
                return this.<DecodeNum>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<DecodeNum>k__BackingField = value;
            }
        }

        internal int[] DecodePos
        {
            [CompilerGenerated]
            get
            {
                return this.<DecodePos>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<DecodePos>k__BackingField = value;
            }
        }

        internal int MaxNum
        {
            [CompilerGenerated]
            get
            {
                return this.<MaxNum>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<MaxNum>k__BackingField = value;
            }
        }
    }
}

