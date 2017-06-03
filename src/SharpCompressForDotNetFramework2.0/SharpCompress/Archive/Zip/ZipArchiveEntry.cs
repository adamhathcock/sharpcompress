using System.IO;
using System.Linq;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archive.Zip
{
    public class ZipArchiveEntry : ZipEntry, IArchiveEntry
    {
        private ZipArchive archive;

        internal ZipArchiveEntry(ZipArchive archive, SeekableZipFilePart part)
            : base(part)
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

        public string Comment
        {
            get
            {
                return (Parts.Single() as SeekableZipFilePart).Comment;
            }
        }
    }
}