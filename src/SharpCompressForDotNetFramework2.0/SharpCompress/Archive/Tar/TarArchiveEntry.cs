using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar;

namespace SharpCompress.Archive.Tar
{
    public class TarArchiveEntry : TarEntry, IArchiveEntry
    {
        private TarArchive archive;
        internal TarArchiveEntry(TarArchive archive, TarFilePart part, CompressionType compressionType)
            : base(part, compressionType)
        {
            this.archive = archive;
        }

        public virtual Stream OpenEntryStream()
        {
            return Parts.Single().GetStream();
        }

        #region IArchiveEntry Members

        public void WriteTo(Stream streamToWriteTo)
        {
            if (IsEncrypted)
            {
                throw new PasswordProtectedException("Entry is password protected and cannot be extracted.");
            }
            this.Extract(archive, streamToWriteTo);
        }

        public bool IsComplete
        {
            get
            {
                return true;
            }
        }

        #endregion
    }
}