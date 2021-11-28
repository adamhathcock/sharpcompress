using System;
using System.IO;
using ZstdSharp;

namespace SharpCompress.Common.ZStandard
{
    internal sealed class ZStandardFilePart : FilePart
    {
        private string _name = "";
        private readonly Stream _stream;

        internal ZStandardFilePart(Stream stream, ArchiveEncoding archiveEncoding)
            : base(archiveEncoding)
        {
            _stream = stream;
            EntryStartPosition = stream.Position;
        }

        internal long EntryStartPosition { get; }

        internal DateTime? DateModified { get; private set; }
        internal int? Crc { get; private set; }
        internal int? UncompressedSize { get; private set; }

        internal override string FilePartName => _name!;

        internal override Stream GetCompressedStream()
        {
            return new DecompressionStream(_stream);
        }

        internal override Stream GetRawStream()
        {
            return _stream;
        }
    }
}
