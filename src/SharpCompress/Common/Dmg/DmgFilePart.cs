using System.IO;

namespace SharpCompress.Common.Dmg
{
    internal sealed class DmgFilePart : FilePart
    {
        private readonly Stream _stream;

        internal override string FilePartName { get; }

        public DmgFilePart(Stream stream, string fileName)
            : base(new ArchiveEncoding())
        {
            _stream = stream;
            FilePartName = fileName;
        }

        internal override Stream GetCompressedStream() => _stream;
        internal override Stream? GetRawStream() => null;
    }
}
