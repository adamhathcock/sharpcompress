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
            LeaveStreamOpen = options.LeaveStreamOpen;
            if (options is ZipWriterOptions)
                UseZip64 = ((ZipWriterOptions)options).UseZip64;
        }
        /// <summary>
        /// When CompressionType.Deflate is used, this property is referenced.  Defaults to CompressionLevel.Default.
        /// </summary>
        public CompressionLevel DeflateCompressionLevel { get; set; } = CompressionLevel.Default;

        public string ArchiveComment { get; set; }

        /// <summary>
        /// Sets a value indicating if zip64 support is enabled. If this is not set, zip64 will be enabled once the file is larger than 2GB
        /// </summary>
        public bool UseZip64 { get; set; }
    }
}