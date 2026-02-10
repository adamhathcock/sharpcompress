using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;
using SharpCompress.Common.SevenZip;

namespace SharpCompress.Archives.SevenZip;

public class SevenZipArchiveEntry : SevenZipEntry, IArchiveEntry
{
    internal SevenZipArchiveEntry(
        SevenZipArchive archive,
        SevenZipFilePart part,
        IReaderOptions readerOptions
    )
        : base(part, readerOptions) => Archive = archive;

    public Stream OpenEntryStream() => FilePart.GetCompressedStream();

    public async ValueTask<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    ) =>
        (
            await FilePart.GetCompressedStreamAsync(cancellationToken).ConfigureAwait(false)
        ).NotNull();

    public IArchive Archive { get; }

    public bool IsComplete => true;

    /// <summary>
    /// This is a 7Zip Anti item
    /// </summary>
    public bool IsAnti => FilePart.Header.IsAnti;
}
