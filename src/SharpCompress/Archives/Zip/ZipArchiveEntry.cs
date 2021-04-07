using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archives.Zip
{
    public class ZipArchiveEntry : ZipEntry, IArchiveEntry
    {
        internal ZipArchiveEntry(ZipArchive archive, SeekableZipFilePart? part)
            : base(part)
        {
            Archive = archive;
        }

        public virtual ValueTask<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
        {
            return Parts.Single().GetCompressedStreamAsync(cancellationToken);
        }

        #region IArchiveEntry Members

        public IArchive Archive { get; }

        public bool IsComplete => true;

        #endregion

        public string? Comment => ((SeekableZipFilePart)Parts.Single()).Comment;
    }
}