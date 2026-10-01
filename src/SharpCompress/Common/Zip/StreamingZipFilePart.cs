using System.IO;
using SharpCompress.Common.Zip.Headers;
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

    internal BinaryReader FixStreamedFileLocation(ref Stream stream)
    {
        if (Header.IsDirectory)
        {
            return new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);
        }

        if (Header.HasData && !Skipped)
        {
            var decompressionStream = _decompressionStream ??= GetCompressedStream().NotNull();

            decompressionStream.Skip();

            // If we had TotalIn / TotalOut we could have used them
            Header.CompressedSize = decompressionStream.Position;

            Skipped = true;
        }
        var reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);
        _decompressionStream = null;
        return reader;
    }
}
