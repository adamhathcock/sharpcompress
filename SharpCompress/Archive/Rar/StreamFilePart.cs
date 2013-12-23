using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Archive.Rar
{
    internal class StreamFilePart : RarFilePart
    {
        private readonly Stream stream;
        internal StreamFilePart(MarkHeader mh, FileHeader fh, Stream stream)
            : base(mh, fh)
        {
            this.stream = stream;
        }

        internal override Stream GetCompressedStream()
        {
            stream.Position = FileHeader.DataStartPosition;
            return stream;
        }

        internal override string FilePartName
        {
            get { return "Unknown Stream - File Entry: " + FileHeader.FileName; }
        }
    }
}