using System.IO;
using SharpCompress.Compressors.Lzw;

namespace SharpCompress.Common.Lzw;

internal sealed partial class LzwFilePart : FilePart
{
    private readonly Stream _stream;

    internal static LzwFilePart Create(Stream stream, IArchiveEncoding archiveEncoding)
    {
        var part = new LzwFilePart(stream, archiveEncoding);

        if (stream.CanSeek)
        {
            part.EntryStartPosition = stream.Position;
        }
        else
        {
            // For non-seekable streams, we can't track position.
            // Set to 0 since the stream will be read sequentially from its current position.
            part.EntryStartPosition = 0;
        }
        return part;
    }

    private LzwFilePart(Stream stream, IArchiveEncoding archiveEncoding)
        : base(archiveEncoding) => _stream = stream;

    internal long EntryStartPosition { get; private set; }

    internal override string? FilePartName => null;

    internal override Stream GetCompressedStream() =>
        new LzwStream(_stream) { IsStreamOwner = false };

    internal override Stream GetRawStream() => _stream;
}
