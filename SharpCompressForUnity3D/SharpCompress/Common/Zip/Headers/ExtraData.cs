namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.Runtime.CompilerServices;

    internal class ExtraData
    {
        [CompilerGenerated]
        private byte[] _DataBytes_k__BackingField;
        [CompilerGenerated]
        private ushort _Length_k__BackingField;
        [CompilerGenerated]
        private ExtraDataType _Type_k__BackingField;

        internal byte[] DataBytes
        {
            [CompilerGenerated]
            get
            {
                return this._DataBytes_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._DataBytes_k__BackingField = value;
            }
        }

        internal ushort Length
        {
            [CompilerGenerated]
            get
            {
                return this._Length_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Length_k__BackingField = value;
            }
        }

        internal ExtraDataType Type
        {
            [CompilerGenerated]
            get
            {
                return this._Type_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Type_k__BackingField = value;
            }
        }
    }
}

