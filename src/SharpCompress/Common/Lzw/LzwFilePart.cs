using System.IO;
using SharpCompress.Compressors.Lzw;

namespace SharpCompress.Common.Lzw;

internal sealed partial class LzwFilePart : FilePart
{
    private readonly Stream _stream;
    private readonly string? _name;

    internal static LzwFilePart Create(Stream stream, IArchiveEncoding archiveEncoding)
    {
        var part = new LzwFilePart(stream, archiveEncoding);

        // For non-seekable streams, we can't track position, so use 0 since the stream will be
        // read sequentially from its current position.
        part.EntryStartPosition = stream.CanSeek ? stream.Position : 0;
        return part;
    }

    private LzwFilePart(Stream stream, IArchiveEncoding archiveEncoding)
        : base(archiveEncoding)
    {
        _stream = stream;
        _name = DeriveFileName(stream);
    }

    internal long EntryStartPosition { get; private set; }

    internal override string? FilePartName => _name;

    internal override Stream GetCompressedStream() =>
        new LzwStream(_stream) { IsStreamOwner = false };

    internal override Stream GetRawStream() => _stream;

    private static string? DeriveFileName(Stream stream)
    {
        // Try to derive filename from FileStream
        if (stream is FileStream fileStream && !string.IsNullOrEmpty(fileStream.Name))
        {
            var fileName = Path.GetFileName(fileStream.Name);
            // Strip .Z extension if present
            if (fileName.EndsWith(".Z", System.StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - 2);
            }
            return fileName;
        }
        // Default name for non-file streams
        return "data";
    }
}
