using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Archive.Rar
{
    internal class SeekableStreamFilePart : RarFilePart
    {
        internal SeekableStreamFilePart(MarkHeader mh, FileHeader fh, Stream stream)
            : base(mh, fh)
        {
            Stream = stream;
        }

        internal Stream Stream
        {
            get;
            private set;
        }

        internal override Stream GetStream()
        {
            Stream.Position = FileHeader.DataStartPosition;
            return Stream;
        }

        internal override string FilePartName
        {
            get
            {
                return "Unknown Stream - File Entry: " + base.FileHeader.FileName;
            }
        }
    }
}
