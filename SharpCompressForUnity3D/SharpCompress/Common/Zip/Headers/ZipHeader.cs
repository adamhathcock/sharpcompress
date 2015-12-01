namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal abstract class ZipHeader
    {
        [CompilerGenerated]
        private bool _HasData_k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Zip.Headers.ZipHeaderType _ZipHeaderType_k__BackingField;

        protected ZipHeader(SharpCompress.Common.Zip.Headers.ZipHeaderType type)
        {
            this.ZipHeaderType = type;
            this.HasData = true;
        }

        internal abstract void Read(BinaryReader reader);
        internal abstract void Write(BinaryWriter writer);

        internal bool HasData
        {
            [CompilerGenerated]
            get
            {
                return this._HasData_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._HasData_k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Zip.Headers.ZipHeaderType ZipHeaderType
        {
            [CompilerGenerated]
            get
            {
                return this._ZipHeaderType_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._ZipHeaderType_k__BackingField = value;
            }
        }
    }
}

