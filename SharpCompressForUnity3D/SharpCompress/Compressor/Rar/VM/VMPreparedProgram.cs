namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal class VMPreparedProgram
    {
        [CompilerGenerated]
        private int <CommandCount>k__BackingField;
        [CompilerGenerated]
        private int <FilteredDataOffset>k__BackingField;
        [CompilerGenerated]
        private int <FilteredDataSize>k__BackingField;
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
                return this.<CommandCount>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<CommandCount>k__BackingField = value;
            }
        }

        internal int FilteredDataOffset
        {
            [CompilerGenerated]
            get
            {
                return this.<FilteredDataOffset>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<FilteredDataOffset>k__BackingField = value;
            }
        }

        internal int FilteredDataSize
        {
            [CompilerGenerated]
            get
            {
                return this.<FilteredDataSize>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<FilteredDataSize>k__BackingField = value;
            }
        }
    }
}

