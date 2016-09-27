using SharpCompress.Common;

namespace SharpCompress.Writers.Tar
{
    public class TarWriterOptions
    {
        public TarWriterOptions()
        {
            
        }

        internal TarWriterOptions(WriterOptions readerOptions)
        {
            LeaveOpenStream = readerOptions.LeaveOpenStream;
            CompressionType = readerOptions.CompressionType;
        }

        public bool LeaveOpenStream { get; set; }

        public CompressionType CompressionType { get; set; } = CompressionType.Unknown;
    }
}