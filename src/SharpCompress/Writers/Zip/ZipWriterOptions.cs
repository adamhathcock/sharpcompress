using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip
{
    public class ZipWriterOptions
    {
        public ZipWriterOptions()
        {
            
        }
        
        internal ZipWriterOptions(WriterOptions options)
        {
            LeaveOpenStream = options.LeaveOpenStream;
            CompressionType = options.CompressionType;
        }

        public bool LeaveOpenStream { get; set; }

        public CompressionType CompressionType { get; set; } = CompressionType.Unknown;
        /// <summary>
        /// When CompressionType.Deflate is used, this property is referenced.  Defaults to CompressionLevel.Default.
        /// </summary>
        public CompressionLevel DeflateCompressionLevel { get; set; } = CompressionLevel.Default;

        public string ArchiveComment { get; set; }
    }
}