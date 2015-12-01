namespace SharpCompress.Compressor.Rar
{
    using SharpCompress.Compressor.Rar.VM;
    using System;
    using System.Runtime.CompilerServices;

    internal class UnpackFilter
    {
        [CompilerGenerated]
        private int _BlockLength_k__BackingField;
        [CompilerGenerated]
        private int _BlockStart_k__BackingField;
        [CompilerGenerated]
        private int _ExecCount_k__BackingField;
        [CompilerGenerated]
        private bool _NextWindow_k__BackingField;
        [CompilerGenerated]
        private int _ParentFilter_k__BackingField;
        [CompilerGenerated]
        private VMPreparedProgram _Program_k__BackingField;

        internal UnpackFilter()
        {
            this.Program = new VMPreparedProgram();
        }

        internal int BlockLength
        {
            [CompilerGenerated]
            get
            {
                return this._BlockLength_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._BlockLength_k__BackingField = value;
            }
        }

        internal int BlockStart
        {
            [CompilerGenerated]
            get
            {
                return this._BlockStart_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._BlockStart_k__BackingField = value;
            }
        }

        internal int ExecCount
        {
            [CompilerGenerated]
            get
            {
                return this._ExecCount_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._ExecCount_k__BackingField = value;
            }
        }

        internal bool NextWindow
        {
            [CompilerGenerated]
            get
            {
                return this._NextWindow_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._NextWindow_k__BackingField = value;
            }
        }

        internal int ParentFilter
        {
            [CompilerGenerated]
            get
            {
                return this._ParentFilter_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._ParentFilter_k__BackingField = value;
            }
        }

        internal VMPreparedProgram Program
        {
            [CompilerGenerated]
            get
            {
                return this._Program_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Program_k__BackingField = value;
            }
        }
    }
}

