using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common
{
    public abstract class FilePart
    {
        protected FilePart(ArchiveEncoding archiveEncoding)
        {
            ArchiveEncoding = archiveEncoding;
        }

        internal ArchiveEncoding ArchiveEncoding { get; }

        internal abstract string FilePartName { get; }

        internal abstract ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken);
        internal abstract Stream? GetRawStream();
        internal bool Skipped { get; set; }
    }
}
