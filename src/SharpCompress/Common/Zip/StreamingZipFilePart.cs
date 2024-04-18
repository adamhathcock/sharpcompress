using System.IO;
using System.Net.Sockets;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed class StreamingZipFilePart : ZipFilePart
{
    private Stream? _decompressionStream;

    internal StreamingZipFilePart(ZipFileEntry header, Stream stream)
        : base(header, stream) { }

    protected override Stream CreateBaseStream() => Header.PackedStream.NotNull();

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

            _decompressionStream.Skip();

            // If we had TotalIn / TotalOut we could have used them
            Header.CompressedSize = _decompressionStream.Position;

            if (_decompressionStream is DeflateStream deflateStream)
            {
                rewindableStream.Rewind(deflateStream.InputBuffer);
            }

            Skipped = true;
        }
        var reader = new BinaryReader(rewindableStream);
        _decompressionStream = null;
        return reader;
    }
}
