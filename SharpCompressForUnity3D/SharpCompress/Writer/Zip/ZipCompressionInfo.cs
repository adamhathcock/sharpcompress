﻿namespace SharpCompress.Writer.Zip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip;
    using SharpCompress.Compressor.Deflate;
    using System;
    using System.Runtime.CompilerServices;

    internal class ZipCompressionInfo
    {
        [CompilerGenerated]
        private ZipCompressionMethod <Compression>k__BackingField;
        [CompilerGenerated]
        private CompressionLevel <DeflateCompressionLevel>k__BackingField;

        public ZipCompressionInfo(CompressionInfo compressionInfo)
        {
            switch (compressionInfo.Type)
            {
                case CompressionType.None:
                    this.Compression = ZipCompressionMethod.None;
                    return;

                case CompressionType.BZip2:
                    this.Compression = ZipCompressionMethod.BZip2;
                    return;

                case CompressionType.PPMd:
                    this.Compression = ZipCompressionMethod.PPMd;
                    return;

                case CompressionType.Deflate:
                    this.DeflateCompressionLevel = compressionInfo.DeflateCompressionLevel;
                    this.Compression = ZipCompressionMethod.Deflate;
                    return;

                case CompressionType.LZMA:
                    this.Compression = ZipCompressionMethod.LZMA;
                    return;
            }
            throw new InvalidFormatException("Invalid compression method: " + compressionInfo.Type);
        }

        internal ZipCompressionMethod Compression
        {
            [CompilerGenerated]
            get
            {
                return this.<Compression>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Compression>k__BackingField = value;
            }
        }

        internal CompressionLevel DeflateCompressionLevel
        {
            [CompilerGenerated]
            get
            {
                return this.<DeflateCompressionLevel>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<DeflateCompressionLevel>k__BackingField = value;
            }
        }
    }
}

