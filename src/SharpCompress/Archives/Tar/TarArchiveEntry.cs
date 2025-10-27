using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;

namespace SharpCompress.Archives.Tar;

public class TarArchiveEntry : TarEntry, IArchiveEntry
{
    internal TarArchiveEntry(TarArchive archive, TarFilePart? part, CompressionType compressionType)
        : base(part, compressionType) => Archive = archive;

    public virtual Stream OpenEntryStream() => Parts.Single().GetCompressedStream().NotNull();

    public virtual Task<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    ) => Task.FromResult(OpenEntryStream());

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
