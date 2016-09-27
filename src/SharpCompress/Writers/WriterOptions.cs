using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers
{
    public class WriterOptions
    {
        public WriterOptions(CompressionType compressionType)
        {
            CompressionType = compressionType;
        }
        public CompressionType CompressionType { get; set; } = CompressionType.Unknown;

        public bool LeaveOpenStream { get; set; }


        /// <summary>
        /// When CompressionType.Deflate is used, this property is referenced.  Defaults to CompressionLevel.Default.
        /// </summary>
        public CompressionLevel DeflateCompressionLevel { get; set; } = CompressionLevel.Default;

        public static implicit operator WriterOptions(CompressionType compressionType)
        {
            return new WriterOptions(compressionType);
        }
    }
}