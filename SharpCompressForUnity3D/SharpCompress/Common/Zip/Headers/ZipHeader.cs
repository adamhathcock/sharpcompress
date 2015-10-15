namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal abstract class ZipHeader
    {
        [CompilerGenerated]
        private bool <HasData>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Zip.Headers.ZipHeaderType <ZipHeaderType>k__BackingField;

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
                return this.<HasData>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<HasData>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Zip.Headers.ZipHeaderType ZipHeaderType
        {
            [CompilerGenerated]
            get
            {
                return this.<ZipHeaderType>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<ZipHeaderType>k__BackingField = value;
            }
        }
    }
}

