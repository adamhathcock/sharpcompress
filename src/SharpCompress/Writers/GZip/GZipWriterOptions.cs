using SharpCompress.Common;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.GZip;

public class GZipWriterOptions : WriterOptions
{
    public GZipWriterOptions()
        : base(CompressionType.GZip, (int)(D.CompressionLevel.Default)) { }

    internal GZipWriterOptions(WriterOptions options)
        : base(options.CompressionType, (int)(D.CompressionLevel.Default))
    {
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        CompressionLevel = options.CompressionLevel;
    }
}
