namespace SharpCompress.Common
{
    using SharpCompress.Compressor.Deflate;
    using System;
    using System.Runtime.CompilerServices;

    public class CompressionInfo
    {
        [CompilerGenerated]
        private CompressionLevel <DeflateCompressionLevel>k__BackingField;
        [CompilerGenerated]
        private CompressionType <Type>k__BackingField;

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
                return this.<DeflateCompressionLevel>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<DeflateCompressionLevel>k__BackingField = value;
            }
        }

        public CompressionType Type
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

