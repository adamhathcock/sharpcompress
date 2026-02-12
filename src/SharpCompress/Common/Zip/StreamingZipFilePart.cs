using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Common.Zip;

internal sealed partial class StreamingZipFilePart : ZipFilePart
{
    private Stream? _decompressionStream;

    internal StreamingZipFilePart(
        ZipFileEntry header,
        Stream stream,
        CompressionProviderRegistry compressionProviders
    )
        : base(header, stream, compressionProviders) { }

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
            return SharpCompressStream.CreateNonDisposing(_decompressionStream);
        }
        return _decompressionStream;
    }

    internal BinaryReader FixStreamedFileLocation(ref Stream stream)
    {
        if (Header.IsDirectory)
        {
            return new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);
        }

        if (Header.HasData && !Skipped)
        {
            _decompressionStream ??= GetCompressedStream();

            _decompressionStream.Skip();

            // If we had TotalIn / TotalOut we could have used them
            Header.CompressedSize = _decompressionStream.Position;

            Skipped = true;
        }
        var reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);
        _decompressionStream = null;
        return reader;
    }
}
