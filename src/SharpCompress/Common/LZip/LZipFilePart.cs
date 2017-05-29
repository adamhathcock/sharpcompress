using System.IO;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Common.LZip
{
    internal class LZipFilePart : FilePart
    {
        private readonly Stream stream;

        internal LZipFilePart(Stream stream)
        {
            if (!LZipStream.IsLZipFile(stream))
            {
                throw new ArchiveException("Stream is not an LZip stream.");
            }
            EntryStartPosition = stream.Position;
            this.stream = stream;
        }

        internal long EntryStartPosition { get; }
        
        internal override string FilePartName => null;

        internal override Stream GetCompressedStream()
        {
            return new LZipStream(stream, CompressionMode.Decompress, false);
        }

        internal override Stream GetRawStream()
        {
            return stream;
        }
    }
}