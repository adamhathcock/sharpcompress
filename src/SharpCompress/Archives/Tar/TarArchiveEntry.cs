using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Common.Tar;

namespace SharpCompress.Archives.Tar;

public class TarArchiveEntry : TarEntry, IArchiveEntry
{
    internal TarArchiveEntry(
        TarArchive archive,
        TarFilePart? part,
        CompressionType compressionType,
        IReaderOptions readerOptions
    )
        : base(part, compressionType, readerOptions) => Archive = archive;

    public virtual Stream OpenEntryStream() => Parts.Single().GetCompressedStream().NotNull();

    public async ValueTask<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    ) =>
        (
            await Parts.Single().GetCompressedStreamAsync(cancellationToken).ConfigureAwait(false)
        ).NotNull();

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
