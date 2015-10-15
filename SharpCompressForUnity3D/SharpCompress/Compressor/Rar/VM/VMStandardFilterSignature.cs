namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Runtime.CompilerServices;

    internal class VMStandardFilterSignature
    {
        [CompilerGenerated]
        private uint _CRC_k__BackingField;
        [CompilerGenerated]
        private int _Length_k__BackingField;
        [CompilerGenerated]
        private VMStandardFilters _Type_k__BackingField;

        internal VMStandardFilterSignature(int length, uint crc, VMStandardFilters type)
        {
            this.Length = length;
            this.CRC = crc;
            this.Type = type;
        }

        internal uint CRC
        {
            [CompilerGenerated]
            get
            {
                return this._CRC_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._CRC_k__BackingField = value;
            }
        }

        internal int Length
        {
            [CompilerGenerated]
            get
            {
                return this._Length_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Length_k__BackingField = value;
            }
        }

        internal VMStandardFilters Type
        {
            [CompilerGenerated]
            get
            {
                return this._Type_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Type_k__BackingField = value;
            }
        }
    }
}

