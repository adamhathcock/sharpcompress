namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Runtime.CompilerServices;

    internal class VMPreparedOperand
    {
        [CompilerGenerated]
        private int _Base_k__BackingField;
        [CompilerGenerated]
        private int _Data_k__BackingField;
        [CompilerGenerated]
        private int _Offset_k__BackingField;
        [CompilerGenerated]
        private VMOpType _Type_k__BackingField;

        internal int Base
        {
            [CompilerGenerated]
            get
            {
                return this._Base_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Base_k__BackingField = value;
            }
        }

        internal int Data
        {
            [CompilerGenerated]
            get
            {
                return this._Data_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Data_k__BackingField = value;
            }
        }

        internal int Offset
        {
            [CompilerGenerated]
            get
            {
                return this._Offset_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Offset_k__BackingField = value;
            }
        }

        internal VMOpType Type
        {
            [CompilerGenerated]
            get
            {
                return this._Type_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Type_k__BackingField = value;
            }
        }
    }
}

