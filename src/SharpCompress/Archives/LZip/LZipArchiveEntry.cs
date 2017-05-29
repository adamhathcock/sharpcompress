using System.IO;
using System.Linq;
using SharpCompress.Common.LZip;

namespace SharpCompress.Archives.LZip
{
    public class LZipArchiveEntry : LZipEntry, IArchiveEntry
    {
        internal LZipArchiveEntry(LZipArchive archive, LZipFilePart part)
            : base(part)
        {
            Archive = archive;
        }

        public virtual Stream OpenEntryStream()
        {
            //this is to reset the stream to be read multiple times
            var part = Parts.Single() as LZipFilePart;
            if (part.GetRawStream().Position != part.EntryStartPosition)
            {
                part.GetRawStream().Position = part.EntryStartPosition;
            }
            return Parts.Single().GetCompressedStream();
        }

        #region IArchiveEntry Members

        public IArchive Archive { get; }

        public bool IsComplete => true;

        #endregion
    }
}