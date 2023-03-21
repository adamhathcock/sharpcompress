using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.GZip;

public class GZipWriterOptions : WriterOptions
{
    public GZipWriterOptions()
        : base(CompressionType.GZip) { }

    internal GZipWriterOptions(WriterOptions options)
        : base(options.CompressionType)
    {
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;

        if (options is GZipWriterOptions writerOptions)
        {
            CompressionLevel = writerOptions.CompressionLevel;
        }
    }

    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Default;
}
