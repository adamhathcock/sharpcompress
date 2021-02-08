using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.GZip;

namespace SharpCompress.Archives.GZip
{
    public class GZipArchiveEntry : GZipEntry, IArchiveEntry
    {
        internal GZipArchiveEntry(GZipArchive archive, GZipFilePart part)
            : base(part)
        {
            Archive = archive;
        }

        public virtual async ValueTask<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
        {
            //this is to reset the stream to be read multiple times
            var part = (GZipFilePart)Parts.Single();
            if (part.GetRawStream().Position != part.EntryStartPosition)
            {
                part.GetRawStream().Position = part.EntryStartPosition;
            }
            return await Parts.Single().GetCompressedStreamAsync(cancellationToken);
        }

        #region IArchiveEntry Members

        public IArchive Archive { get; }

        public bool IsComplete => true;

        #endregion
    }
}