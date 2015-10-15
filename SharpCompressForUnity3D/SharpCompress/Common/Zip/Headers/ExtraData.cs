namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.Runtime.CompilerServices;

    internal class ExtraData
    {
        [CompilerGenerated]
        private byte[] <DataBytes>k__BackingField;
        [CompilerGenerated]
        private ushort <Length>k__BackingField;
        [CompilerGenerated]
        private ExtraDataType <Type>k__BackingField;

        internal byte[] DataBytes
        {
            [CompilerGenerated]
            get
            {
                return this.<DataBytes>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<DataBytes>k__BackingField = value;
            }
        }

        internal ushort Length
        {
            [CompilerGenerated]
            get
            {
                return this.<Length>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Length>k__BackingField = value;
            }
        }

        internal ExtraDataType Type
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

