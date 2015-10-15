namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Runtime.CompilerServices;

    internal class VMPreparedCommand
    {
        [CompilerGenerated]
        private bool <IsByteMode>k__BackingField;
        [CompilerGenerated]
        private VMPreparedOperand <Op1>k__BackingField;
        [CompilerGenerated]
        private VMPreparedOperand <Op2>k__BackingField;
        [CompilerGenerated]
        private VMCommands <OpCode>k__BackingField;

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
                return this.<IsByteMode>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<IsByteMode>k__BackingField = value;
            }
        }

        internal VMPreparedOperand Op1
        {
            [CompilerGenerated]
            get
            {
                return this.<Op1>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Op1>k__BackingField = value;
            }
        }

        internal VMPreparedOperand Op2
        {
            [CompilerGenerated]
            get
            {
                return this.<Op2>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Op2>k__BackingField = value;
            }
        }

        internal VMCommands OpCode
        {
            [CompilerGenerated]
            get
            {
                return this.<OpCode>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<OpCode>k__BackingField = value;
            }
        }
    }
}

