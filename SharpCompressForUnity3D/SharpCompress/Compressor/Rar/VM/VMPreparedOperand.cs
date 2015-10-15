namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Runtime.CompilerServices;

    internal class VMPreparedOperand
    {
        [CompilerGenerated]
        private int <Base>k__BackingField;
        [CompilerGenerated]
        private int <Data>k__BackingField;
        [CompilerGenerated]
        private int <Offset>k__BackingField;
        [CompilerGenerated]
        private VMOpType <Type>k__BackingField;

        internal int Base
        {
            [CompilerGenerated]
            get
            {
                return this.<Base>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Base>k__BackingField = value;
            }
        }

        internal int Data
        {
            [CompilerGenerated]
            get
            {
                return this.<Data>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Data>k__BackingField = value;
            }
        }

        internal int Offset
        {
            [CompilerGenerated]
            get
            {
                return this.<Offset>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Offset>k__BackingField = value;
            }
        }

        internal VMOpType Type
        {
            [CompilerGenerated]
            get
            {
                return this.<Type>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Type>k__BackingField = value;
            }
        }
    }
}

