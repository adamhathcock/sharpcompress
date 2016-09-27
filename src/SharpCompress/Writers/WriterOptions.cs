using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers
{
    public class WriterOptions : OptionsBase
    {
        public WriterOptions(CompressionType compressionType)
        {
            CompressionType = compressionType;
        }
        public CompressionType CompressionType { get; set; }

        public static implicit operator WriterOptions(CompressionType compressionType)
        {
            return new WriterOptions(compressionType);
        }
    }
}