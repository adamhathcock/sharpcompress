namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Runtime.CompilerServices;

    internal class VMPreparedCommand
    {
        [CompilerGenerated]
        private bool _IsByteMode_k__BackingField;
        [CompilerGenerated]
        private VMPreparedOperand _Op1_k__BackingField;
        [CompilerGenerated]
        private VMPreparedOperand _Op2_k__BackingField;
        [CompilerGenerated]
        private VMCommands _OpCode_k__BackingField;

        internal VMPreparedCommand()
        {
            this.Op1 = new VMPreparedOperand();
            this.Op2 = new VMPreparedOperand();
        }

        internal bool IsByteMode
        {
            [CompilerGenerated]
            get
            {
                return this._IsByteMode_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._IsByteMode_k__BackingField = value;
            }
        }

        internal VMPreparedOperand Op1
        {
            [CompilerGenerated]
            get
            {
                return this._Op1_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Op1_k__BackingField = value;
            }
        }

        internal VMPreparedOperand Op2
        {
            [CompilerGenerated]
            get
            {
                return this._Op2_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Op2_k__BackingField = value;
            }
        }

        internal VMCommands OpCode
        {
            [CompilerGenerated]
            get
            {
                return this._OpCode_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._OpCode_k__BackingField = value;
            }
        }
    }
}

