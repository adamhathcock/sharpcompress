using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Archive.Rar
{
    internal class FileInfoRarFilePart : RarFilePart
    {
        private FileInfoRarArchiveVolume volume;

        internal FileInfoRarFilePart(FileInfoRarArchiveVolume volume, MarkHeader mh, FileHeader fh, FileInfo fi)
            : base(mh, fh)
        {
            this.volume = volume;
            FileInfo = fi;
        }

        internal FileInfo FileInfo
        {
            get;
            private set;
        }

        internal override Stream GetStream()
        {
            var stream = volume.Stream;
            stream.Position = FileHeader.DataStartPosition;
            return stream;
        }

        internal override string FilePartName
        {
            get
            {
                return "Rar File: " + FileInfo.FullName
                    + " File Entry: " + FileHeader.FileName;
            }
        }
    }
}
