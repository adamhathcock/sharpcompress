namespace SharpCompress.Compressor.Rar.decode
{
    using System;
    using System.Runtime.CompilerServices;

    internal class AudioVariables
    {
        [CompilerGenerated]
        private int _ByteCount_k__BackingField;
        [CompilerGenerated]
        private int _D1_k__BackingField;
        [CompilerGenerated]
        private int _D2_k__BackingField;
        [CompilerGenerated]
        private int _D3_k__BackingField;
        [CompilerGenerated]
        private int _D4_k__BackingField;
        [CompilerGenerated]
        private int[] _Dif_k__BackingField;
        [CompilerGenerated]
        private int _K1_k__BackingField;
        [CompilerGenerated]
        private int _K2_k__BackingField;
        [CompilerGenerated]
        private int _K3_k__BackingField;
        [CompilerGenerated]
        private int _K4_k__BackingField;
        [CompilerGenerated]
        private int _K5_k__BackingField;
        [CompilerGenerated]
        private int _LastChar_k__BackingField;
        [CompilerGenerated]
        private int _LastDelta_k__BackingField;

        internal AudioVariables()
        {
            this.Dif = new int[11];
        }

        internal int ByteCount
        {
            [CompilerGenerated]
            get
            {
                return this._ByteCount_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._ByteCount_k__BackingField = value;
            }
        }

        internal int D1
        {
            [CompilerGenerated]
            get
            {
                return this._D1_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._D1_k__BackingField = value;
            }
        }

        internal int D2
        {
            [CompilerGenerated]
            get
            {
                return this._D2_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._D2_k__BackingField = value;
            }
        }

        internal int D3
        {
            [CompilerGenerated]
            get
            {
                return this._D3_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._D3_k__BackingField = value;
            }
        }

        internal int D4
        {
            [CompilerGenerated]
            get
            {
                return this._D4_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._D4_k__BackingField = value;
            }
        }

        internal int[] Dif
        {
            [CompilerGenerated]
            get
            {
                return this._Dif_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Dif_k__BackingField = value;
            }
        }

        internal int K1
        {
            [CompilerGenerated]
            get
            {
                return this._K1_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._K1_k__BackingField = value;
            }
        }

        internal int K2
        {
            [CompilerGenerated]
            get
            {
                return this._K2_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._K2_k__BackingField = value;
            }
        }

        internal int K3
        {
            [CompilerGenerated]
            get
            {
                return this._K3_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._K3_k__BackingField = value;
            }
        }

        internal int K4
        {
            [CompilerGenerated]
            get
            {
                return this._K4_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._K4_k__BackingField = value;
            }
        }

        internal int K5
        {
            [CompilerGenerated]
            get
            {
                return this._K5_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._K5_k__BackingField = value;
            }
        }

        internal int LastChar
        {
            [CompilerGenerated]
            get
            {
                return this._LastChar_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._LastChar_k__BackingField = value;
            }
        }

        internal int LastDelta
        {
            [CompilerGenerated]
            get
            {
                return this._LastDelta_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._LastDelta_k__BackingField = value;
            }
        }
    }
}

