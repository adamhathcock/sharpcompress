namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal class VMPreparedProgram
    {
        [CompilerGenerated]
        private int _CommandCount_k__BackingField;
        [CompilerGenerated]
        private int _FilteredDataOffset_k__BackingField;
        [CompilerGenerated]
        private int _FilteredDataSize_k__BackingField;
        internal List<VMPreparedCommand> AltCommands = new List<VMPreparedCommand>();
        internal List<VMPreparedCommand> Commands = new List<VMPreparedCommand>();
        internal List<byte> GlobalData = new List<byte>();
        internal int[] InitR = new int[7];
        internal List<byte> StaticData = new List<byte>();

        public int CommandCount
        {
            [CompilerGenerated]
            get
            {
                return this._CommandCount_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._CommandCount_k__BackingField = value;
            }
        }

        internal int FilteredDataOffset
        {
            [CompilerGenerated]
            get
            {
                return this._FilteredDataOffset_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._FilteredDataOffset_k__BackingField = value;
            }
        }

        internal int FilteredDataSize
        {
            [CompilerGenerated]
            get
            {
                return this._FilteredDataSize_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._FilteredDataSize_k__BackingField = value;
            }
        }
    }
}

