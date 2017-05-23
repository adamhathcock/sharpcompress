using System.IO;
using System.Linq;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archives.Zip
{
    public class ZipArchiveEntry : ZipEntry, IArchiveEntry
    {
        internal ZipArchiveEntry(ZipArchive archive, SeekableZipFilePart part)
            : base(part)
        {
            Archive = archive;
        }

        public virtual Stream OpenEntryStream()
        {
            var filePart = Parts.Single() as ZipFilePart;
            var compressionMethod = filePart.Header.CompressionMethod;
            var stream = filePart.GetCompressedStream();
            if (filePart.Header.CompressionMethod != compressionMethod)
            {
                filePart.Header.CompressionMethod = compressionMethod;
            }
            
            return stream; 
        }

        #region IArchiveEntry Members

        public IArchive Archive { get; }

        public bool IsComplete => true;

        #endregion

        public string Comment => (Parts.Single() as SeekableZipFilePart).Comment;
    }
}