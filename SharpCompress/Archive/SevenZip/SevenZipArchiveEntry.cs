using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;

namespace SharpCompress.Archive.SevenZip
{
    public class SevenZipArchiveEntry : SevenZipEntry, IArchiveEntry
    {
        private SevenZipArchive archive;

        internal SevenZipArchiveEntry(SevenZipArchive archive, SevenZipFilePart part)
            : base(part)
        {
            this.archive = archive;
        }

        public Stream OpenEntryStream()
        {
            return FilePart.GetCompressedStream();
        }

        public void WriteTo(Stream stream)
        {
            if (IsEncrypted)
            {
                throw new PasswordProtectedException("Entry is password protected and cannot be extracted.");
            }
            this.Extract(archive, stream);
        }

        public bool IsComplete
        {
            get { return true; }
        }

        /// <summary>
        /// This is a 7Zip Anti item
        /// </summary>
        public bool IsAnti
        {
            get { return FilePart.Header.IsAnti; }
        }
    }
}