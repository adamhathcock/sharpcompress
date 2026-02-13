using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.GZip;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives.GZip;

public class GZipArchiveEntry : GZipEntry, IArchiveEntry
{
    internal GZipArchiveEntry(GZipArchive archive, GZipFilePart? part, IReaderOptions readerOptions)
        : base(part, readerOptions) => Archive = archive;

    public virtual Stream OpenEntryStream()
    {
        //this is to reset the stream to be read multiple times
        var part = (GZipFilePart)Parts.Single();
        var rawStream = part.GetRawStream();
        if (rawStream.CanSeek && rawStream.Position != part.EntryStartPosition)
        {
            rawStream.Position = part.EntryStartPosition;
        }
        return Parts.Single().GetCompressedStream().NotNull();
    }

    public async ValueTask<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        // Reset the stream position if seekable
        var part = (GZipFilePart)Parts.Single();
        var rawStream = part.GetRawStream();
        if (rawStream.CanSeek && rawStream.Position != part.EntryStartPosition)
        {
            rawStream.Position = part.EntryStartPosition;
        }
        return (
            await Parts.Single().GetCompressedStreamAsync(cancellationToken).ConfigureAwait(false)
        ).NotNull();
    }

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
