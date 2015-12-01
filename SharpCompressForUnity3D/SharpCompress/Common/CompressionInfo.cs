namespace SharpCompress.Common
{
    using SharpCompress.Compressor.Deflate;
    using System;
    using System.Runtime.CompilerServices;

    public class CompressionInfo
    {
        [CompilerGenerated]
        private CompressionLevel _DeflateCompressionLevel_k__BackingField;
        [CompilerGenerated]
        private CompressionType _Type_k__BackingField;

        public CompressionInfo()
        {
            this.DeflateCompressionLevel = CompressionLevel.Default;
        }

        public static implicit operator CompressionInfo(CompressionType compressionType)
        {
            CompressionInfo info = new CompressionInfo();
            info.Type = compressionType;
            return info;
        }

        public CompressionLevel DeflateCompressionLevel
        {
            [CompilerGenerated]
            get
            {
                return this._DeflateCompressionLevel_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._DeflateCompressionLevel_k__BackingField = value;
            }
        }

        public CompressionType Type
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

