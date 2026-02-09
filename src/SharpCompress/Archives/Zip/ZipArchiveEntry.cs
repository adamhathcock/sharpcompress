using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archives.Zip;

public partial class ZipArchiveEntry : ZipEntry, IArchiveEntry
{
    internal ZipArchiveEntry(
        ZipArchive archive,
        SeekableZipFilePart? part,
        IReaderOptions readerOptions
    )
        : base(part, readerOptions) => Archive = archive;

    public virtual Stream OpenEntryStream() => Parts.Single().GetCompressedStream().NotNull();

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
