using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archives.Zip;

public class ZipArchiveEntry : ZipEntry, IArchiveEntry
{
    internal ZipArchiveEntry(ZipArchive archive, SeekableZipFilePart? part)
        : base(part) => Archive = archive;

    public virtual Stream OpenEntryStream() => Parts.Single().GetCompressedStream().NotNull();

    public async ValueTask<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        var part = Parts.Single();
        if (part is SeekableZipFilePart seekablePart)
        {
            return (await seekablePart.GetCompressedStreamAsync(cancellationToken)).NotNull();
        }
        return OpenEntryStream();
    }

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
