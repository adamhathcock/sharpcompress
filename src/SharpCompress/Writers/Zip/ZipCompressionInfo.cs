using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip
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
                    Compression = ZipCompressionMethod.None;
                }
                    break;
                case CompressionType.Deflate:
                {
                    DeflateCompressionLevel = compressionInfo.DeflateCompressionLevel;
                    Compression = ZipCompressionMethod.Deflate;
                }
                    break;
                case CompressionType.BZip2:
                {
                    Compression = ZipCompressionMethod.BZip2;
                }
                    break;
                case CompressionType.LZMA:
                {
                    Compression = ZipCompressionMethod.LZMA;
                }
                    break;
                case CompressionType.PPMd:
                {
                    Compression = ZipCompressionMethod.PPMd;
                }
                    break;
                default:
                    throw new InvalidFormatException("Invalid compression method: " + compressionInfo.Type);
            }
        }
    }
}