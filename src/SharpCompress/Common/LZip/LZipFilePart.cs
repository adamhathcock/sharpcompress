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
            DictionarySize = LZipStream.ValidateAndReadSize(stream);
            if (DictionarySize == 0)
            {
                throw new ArchiveException("Stream is not an LZip stream.");
            }
            EntryStartPosition = stream.Position;
            this.stream = stream;
        }

        internal long EntryStartPosition { get; }

        internal int DictionarySize { get; }
        
        internal override string FilePartName => LZipEntry.LZIP_FILE_NAME;

        internal override Stream GetCompressedStream()
        {
            return new LZipStream(stream, CompressionMode.Decompress, DictionarySize, false);
        }

        internal override Stream GetRawStream()
        {
            return stream;
        }
    }
}