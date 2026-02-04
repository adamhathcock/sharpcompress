using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Tar.Headers;

namespace SharpCompress.Common.Tar;

internal sealed class TarFilePart : FilePart
{
    private readonly Stream? _seekableStream;

    internal TarFilePart(TarHeader header, Stream? seekableStream)
        : base(header.ArchiveEncoding)
    {
        _seekableStream = seekableStream;
        Header = header;
    }

    internal TarHeader Header { get; }

    internal override string? FilePartName => Header?.Name;

    internal override Stream GetCompressedStream()
    {
        if (_seekableStream != null)
        {
            _seekableStream.Position = Header.DataStartPosition ?? 0;
            return new TarReadOnlySubStream(_seekableStream, Header.Size, false);
        }
        return Header.PackedStream.NotNull();
    }

    internal override ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_seekableStream != null)
        {
            var useSyncOverAsync = false;
#if LEGACY_DOTNET
            useSyncOverAsync = true;
#endif
            _seekableStream.Position = Header.DataStartPosition ?? 0;
            return new ValueTask<Stream?>(
                new TarReadOnlySubStream(_seekableStream, Header.Size, useSyncOverAsync)
            );
        }
        return new ValueTask<Stream?>(Header.PackedStream.NotNull());
    }

    internal override Stream? GetRawStream() => null;
}
