using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

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
        if (_seekableStream is not null)
        {
            // If the seekable stream is a SourceStream in file mode with multi-threading enabled,
            // create an independent stream to support concurrent extraction
            if (
                _seekableStream is SourceStream sourceStream
                && sourceStream.IsFileMode
                && sourceStream.ReaderOptions.EnableMultiThreadedExtraction
            )
            {
                var independentStream = sourceStream.CreateIndependentStream(0);
                if (independentStream is not null)
                {
                    independentStream.Position = Header.DataStartPosition ?? 0;
                    return new TarReadOnlySubStream(independentStream, Header.Size);
                }
            }

            // Check if the seekable stream wraps a FileStream
            Stream? underlyingStream = _seekableStream;
            if (_seekableStream is IStreamStack streamStack)
            {
                underlyingStream = streamStack.BaseStream();
            }

            if (underlyingStream is FileStream fileStream)
            {
                // Create a new independent stream from the file
                var independentStream = new FileStream(
                    fileStream.Name,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                independentStream.Position = Header.DataStartPosition ?? 0;
                return new TarReadOnlySubStream(independentStream, Header.Size);
            }

            // Fall back to existing behavior for stream-based sources
            _seekableStream.Position = Header.DataStartPosition ?? 0;
            return new TarReadOnlySubStream(_seekableStream, Header.Size);
        }
        return Header.PackedStream.NotNull();
    }

    internal override Stream? GetRawStream() => null;
}
