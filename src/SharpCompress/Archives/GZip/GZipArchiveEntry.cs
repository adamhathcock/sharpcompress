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

    public ValueTask<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
    {
        // GZip synchronous implementation is fast enough, just wrap it
        return new(OpenEntryStream());
    }

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
