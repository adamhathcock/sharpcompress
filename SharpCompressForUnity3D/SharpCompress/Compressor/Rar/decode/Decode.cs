namespace SharpCompress.Compressor.Rar.decode
{
    using System;
    using System.Runtime.CompilerServices;

    internal class Decode
    {
        [CompilerGenerated]
        private int[] _DecodeLen_k__BackingField;
        [CompilerGenerated]
        private int[] _DecodeNum_k__BackingField;
        [CompilerGenerated]
        private int[] _DecodePos_k__BackingField;
        [CompilerGenerated]
        private int _MaxNum_k__BackingField;

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
                return this._DecodeLen_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._DecodeLen_k__BackingField = value;
            }
        }

        internal int[] DecodeNum
        {
            [CompilerGenerated]
            get
            {
                return this._DecodeNum_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._DecodeNum_k__BackingField = value;
            }
        }

        internal int[] DecodePos
        {
            [CompilerGenerated]
            get
            {
                return this._DecodePos_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._DecodePos_k__BackingField = value;
            }
        }

        internal int MaxNum
        {
            [CompilerGenerated]
            get
            {
                return this._MaxNum_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._MaxNum_k__BackingField = value;
            }
        }
    }
}

