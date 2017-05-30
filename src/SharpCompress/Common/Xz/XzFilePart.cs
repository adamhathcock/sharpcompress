using System;
using System.IO;

namespace SharpCompress.Common.Xz
{
    internal class XzFilePart : FilePart
    {
        private string name;
        private readonly Stream stream;

        internal XzFilePart(Stream stream)
        {
            EntryStartPosition = stream.Position;
            this.stream = stream;
        }

        internal long EntryStartPosition { get; }

        internal DateTime? DateModified { get; private set; }

        internal override string FilePartName => name;

        internal override Stream GetCompressedStream()
        {
            throw new NotImplementedException();
        }

        internal override Stream GetRawStream()
        {
            return stream;
        }
    }
}