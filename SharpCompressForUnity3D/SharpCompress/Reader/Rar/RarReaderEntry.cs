namespace SharpCompress.Reader.Rar
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class RarReaderEntry : RarEntry
    {
        [CompilerGenerated]
        private RarFilePart _Part_k__BackingField;

        internal RarReaderEntry(bool solid, RarFilePart part)
        {
            this.Part = part;
            base.IsSolid = solid;
        }

        public override long CompressedSize
        {
            get
            {
                return this.Part.FileHeader.CompressedSize;
            }
        }

        public override SharpCompress.Common.CompressionType CompressionType
        {
            get
            {
                return SharpCompress.Common.CompressionType.Rar;
            }
        }

        internal override SharpCompress.Common.Rar.Headers.FileHeader FileHeader
        {
            get
            {
                return this.Part.FileHeader;
            }
        }

        internal RarFilePart Part
        {
            [CompilerGenerated]
            get
            {
                return this._Part_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Part_k__BackingField = value;
            }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get
            {
                return Utility.AsEnumerable<FilePart>(this.Part);
            }
        }

        public override long Size
        {
            get
            {
                return this.Part.FileHeader.UncompressedSize;
            }
        }
    }
}

