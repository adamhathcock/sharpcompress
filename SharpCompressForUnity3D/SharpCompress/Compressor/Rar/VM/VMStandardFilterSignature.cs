namespace SharpCompress.Compressor.Rar.VM
{
    using System;
    using System.Runtime.CompilerServices;

    internal class VMStandardFilterSignature
    {
        [CompilerGenerated]
        private uint <CRC>k__BackingField;
        [CompilerGenerated]
        private int <Length>k__BackingField;
        [CompilerGenerated]
        private VMStandardFilters <Type>k__BackingField;

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
                return this.<CRC>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<CRC>k__BackingField = value;
            }
        }

        internal int Length
        {
            [CompilerGenerated]
            get
            {
                return this.<Length>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Length>k__BackingField = value;
            }
        }

        internal VMStandardFilters Type
        {
            [CompilerGenerated]
            get
            {
                return this.<Type>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Type>k__BackingField = value;
            }
        }
    }
}

