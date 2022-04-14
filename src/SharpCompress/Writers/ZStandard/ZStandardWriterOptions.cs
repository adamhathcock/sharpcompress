using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.ZStandard
{
    public class ZStandardWriterOptions : WriterOptions
    {
        public ZStandardWriterOptions()
            : base(CompressionType.ZStandard)
        {
        }

        internal ZStandardWriterOptions(WriterOptions options)
            : base(options.CompressionType)
        {
            LeaveStreamOpen = options.LeaveStreamOpen;
            ArchiveEncoding = options.ArchiveEncoding;

            var writerOptions = options as ZStandardWriterOptions;
            if (writerOptions != null)
            {
                CompressionLevel = writerOptions.CompressionLevel;
            }
        }

        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Default;
    }
}