using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed class StreamingZipFilePart : ZipFilePart
{
    private Stream? _decompressionStream;

    internal StreamingZipFilePart(ZipFileEntry header, Stream stream) : base(header, stream) { }

    protected override Stream CreateBaseStream() => Header.PackedStream;

    internal override Stream GetCompressedStream()
    {
        if (!Header.HasData)
        {
            return Stream.Null;
        }
        _decompressionStream = CreateDecompressionStream(
            GetCryptoStream(CreateBaseStream()),
            Header.CompressionMethod
        );
        if (LeaveStreamOpen)
        {
            return NonDisposingStream.Create(_decompressionStream);
        }
        return _decompressionStream;
    }

    internal BinaryReader FixStreamedFileLocation(ref RewindableStream rewindableStream)
    {
        if (Header.IsDirectory)
        {
            return new BinaryReader(rewindableStream);
        }
        if (Header.HasData && !Skipped)
        {
            _decompressionStream ??= GetCompressedStream();

            if (Header.CompressionMethod != ZipCompressionMethod.None)
            {
                _decompressionStream.Skip();

                // If we had TotalIn / TotalOut we could have used them
                Header.CompressedSize = _decompressionStream.Position;

                if (_decompressionStream is DeflateStream deflateStream)
                {
                    rewindableStream.Rewind(deflateStream.InputBuffer);
                }
            }
            else
            {
                // We would need to search for the magic word
                rewindableStream.Position -= 4;
                var pos = rewindableStream.Position;
                while (Utility.Find(rewindableStream, new byte[] { 0x50, 0x4b, 0x07, 0x08 }))
                {
                    // We should probably check CRC32 for positive matching as well
                    var size = rewindableStream.Position - pos;
                    var br = new BinaryReader(rewindableStream);
                    br.ReadUInt32();
                    br.ReadUInt32(); // CRC32
                    var compressed_size = br.ReadUInt32();
                    var uncompressed_size = br.ReadUInt32();
                    var uncompressed_64bit = br.ReadInt64();

                    var test_64bit = ((long)uncompressed_size << 32) | compressed_size;

                    if (test_64bit == size && test_64bit == uncompressed_64bit)
                    {
                        Header.CompressedSize = test_64bit;
                        Header.UncompressedSize = uncompressed_64bit;
                        rewindableStream.Position -= 24;
                        break;
                    }

                    if (compressed_size == size && compressed_size == uncompressed_size)
                    {
                        Header.CompressedSize = compressed_size;
                        Header.UncompressedSize = uncompressed_size;
                        rewindableStream.Position -= 24;
                        break;
                    }
                    rewindableStream.Position -= 20;
                }
            }

            Skipped = true;
        }
        var reader = new BinaryReader(rewindableStream);
        _decompressionStream = null;
        return reader;
    }
}
