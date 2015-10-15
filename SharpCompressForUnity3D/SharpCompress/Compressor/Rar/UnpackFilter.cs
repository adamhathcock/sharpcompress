namespace SharpCompress.Compressor.Rar
{
    using SharpCompress.Compressor.Rar.VM;
    using System;
    using System.Runtime.CompilerServices;

    internal class UnpackFilter
    {
        [CompilerGenerated]
        private int <BlockLength>k__BackingField;
        [CompilerGenerated]
        private int <BlockStart>k__BackingField;
        [CompilerGenerated]
        private int <ExecCount>k__BackingField;
        [CompilerGenerated]
        private bool <NextWindow>k__BackingField;
        [CompilerGenerated]
        private int <ParentFilter>k__BackingField;
        [CompilerGenerated]
        private VMPreparedProgram <Program>k__BackingField;

        internal UnpackFilter()
        {
            this.Program = new VMPreparedProgram();
        }

        internal int BlockLength
        {
            [CompilerGenerated]
            get
            {
                return this.<BlockLength>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<BlockLength>k__BackingField = value;
            }
        }

        internal int BlockStart
        {
            [CompilerGenerated]
            get
            {
                return this.<BlockStart>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<BlockStart>k__BackingField = value;
            }
        }

        internal int ExecCount
        {
            [CompilerGenerated]
            get
            {
                return this.<ExecCount>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<ExecCount>k__BackingField = value;
            }
        }

        internal bool NextWindow
        {
            [CompilerGenerated]
            get
            {
                return this.<NextWindow>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<NextWindow>k__BackingField = value;
            }
        }

        internal int ParentFilter
        {
            [CompilerGenerated]
            get
            {
                return this.<ParentFilter>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<ParentFilter>k__BackingField = value;
            }
        }

        internal VMPreparedProgram Program
        {
            [CompilerGenerated]
            get
            {
                return this.<Program>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Program>k__BackingField = value;
            }
        }
    }
}

