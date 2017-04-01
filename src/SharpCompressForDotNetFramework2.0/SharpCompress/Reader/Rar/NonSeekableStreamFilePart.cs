using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Reader.Rar
{
    internal class NonSeekableStreamFilePart : RarFilePart
    {
        internal NonSeekableStreamFilePart(MarkHeader mh, FileHeader fh)
            : base(mh, fh)
        {
        }

        internal override Stream GetStream()
        {
            return FileHeader.PackedStream;
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
