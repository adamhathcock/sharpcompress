using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip
{
    public class ZipWriterOptions : WriterOptions
    {
        public ZipWriterOptions(CompressionType compressionType)
            : base(compressionType)
        {
        }

        internal ZipWriterOptions(WriterOptions options)
            : base(options.CompressionType)
        {
            LeaveOpenStream = options.LeaveOpenStream;
        }
        /// <summary>
        /// When CompressionType.Deflate is used, this property is referenced.  Defaults to CompressionLevel.Default.
        /// </summary>
        public CompressionLevel DeflateCompressionLevel { get; set; } = CompressionLevel.Default;

        public string ArchiveComment { get; set; }
    }
}