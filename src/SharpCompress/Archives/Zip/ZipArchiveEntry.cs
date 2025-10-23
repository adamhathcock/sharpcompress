using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archives.Zip;

public class ZipArchiveEntry : ZipEntry, IArchiveEntry
{
    internal ZipArchiveEntry(ZipArchive archive, SeekableZipFilePart? part)
        : base(part) => Archive = archive;

    public virtual Stream OpenEntryStream() => Parts.Single().GetCompressedStream().NotNull();

    public virtual async Task<Stream> OpenEntryStreamAsync() =>
        await Task.FromResult(OpenEntryStream());

    #region IArchiveEntry Members

    public IArchive Archive { get; }

    public bool IsComplete => true;

    #endregion
}
