using System.IO;
using System.Linq;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip
{
    internal class SevenZipFilePart : FilePart
    {
        private CompressionType? type;

        internal SevenZipFilePart(SevenZipHeaderFactory factory, int index, HeaderEntry fileEntry, Stream stream)
        {
            Header = fileEntry;
            this.BaseStream = stream;
        }

        internal Stream BaseStream { get; private set; }
        internal HeaderEntry Header { get; set; }

        internal override string FilePartName
        {
            get { return Header.Name; }
        }

        internal override Stream GetStream()
        {
            if (!Header.HasStream)
            {
                return null;
            }
            var stream = Header.Folder.GetStream();
            if (Header.FolderOffset > 0)
            {
                stream.Skip((long)Header.FolderOffset);
            }
            return new ReadOnlySubStream(stream, (long)Header.Size);
        }

        public CompressionType CompressionType
        {
            get
            {
                if (type == null)
                {
                    this.type = Header.Folder.GetCompressions().First().CompressionType;
                }
                return type.Value;
            }
        }
    }
}
