using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Compressor.Deflate;

namespace SharpCompress.Writer.Zip
{
    internal class ZipCompressionInfo
    {
        internal CompressionLevel DeflateCompressionLevel { get; private set; }
        internal ZipCompressionMethod Compression { get; private set; }

        public ZipCompressionInfo(CompressionInfo compressionInfo)
        {
            switch (compressionInfo.Type)
            {
                case CompressionType.None:
                    {
                        this.Compression = ZipCompressionMethod.None;
                    }
                    break;
                case CompressionType.Deflate:
                    {
                        this.DeflateCompressionLevel = compressionInfo.DeflateCompressionLevel;
                        this.Compression = ZipCompressionMethod.Deflate;
                    }
                    break;
                case CompressionType.BZip2:
                    {
                        this.Compression = ZipCompressionMethod.BZip2;
                    }
                    break;
                case CompressionType.LZMA:
                    {
                        this.Compression = ZipCompressionMethod.LZMA;
                    }
                    break;
                case CompressionType.PPMd:
                    {
                        this.Compression = ZipCompressionMethod.PPMd;
                    }
                    break;
                default:
                    throw new InvalidFormatException("Invalid compression method: " + compressionInfo.Type);
            }
        }
    }
}
