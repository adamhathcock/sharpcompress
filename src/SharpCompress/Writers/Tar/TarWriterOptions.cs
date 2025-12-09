using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;

namespace SharpCompress.Writers.Tar;

public class TarWriterOptions : WriterOptions
{
    /// <summary>
    /// Indicates if archive should be finalized (by 2 empty blocks) on close.
    /// </summary>
    public bool FinalizeArchiveOnClose { get; }

    public TarHeaderWriteFormat HeaderFormat { get; }

    public TarWriterOptions(
        CompressionType compressionType,
        bool finalizeArchiveOnClose,
        TarHeaderWriteFormat headerFormat = TarHeaderWriteFormat.GNU_TAR_LONG_LINK
    )
        : base(compressionType)
    {
        FinalizeArchiveOnClose = finalizeArchiveOnClose;
        HeaderFormat = headerFormat;
    }

    internal TarWriterOptions(WriterOptions options)
        : this(options.CompressionType, true) => ArchiveEncoding = options.ArchiveEncoding;
}
