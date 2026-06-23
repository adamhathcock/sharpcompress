using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Providers;

namespace SharpCompress.Compressors.BZip2;

public sealed partial class BZip2Stream : Stream, IFinishable
{
    private Stream stream = default!;
    private bool isDisposed;

    private BZip2Stream() { }

    /// <summary>
    /// Create a BZip2Stream
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="compressionMode">Compression Mode</param>
    /// <param name="decompressConcatenated">Decompress Concatenated</param>
    /// <param name="leaveOpen">Leave the underlying stream open when this stream is disposed</param>
    /// <param name="tolerateTruncatedStream">
    /// Decompression only. When true, an end-of-stream reached at a bzip2 block boundary is treated as a
    /// normal end of stream rather than throwing. This allows decoding a truncated or partial stream - for
    /// example a sub-range of blocks extracted for random access - that has no trailing stream footer. EOF
    /// in the middle of a block is still reported as an error. Because a partial decode's running combined
    /// CRC won't match the whole-stream value stored in the footer, that whole-stream CRC is not verified
    /// in this mode (per-block CRCs are still checked).
    /// </param>
    public static BZip2Stream Create(
        Stream stream,
        CompressionMode compressionMode,
        bool decompressConcatenated,
        bool leaveOpen = false,
        bool tolerateTruncatedStream = false
    ) =>
        Create(
            stream,
            compressionMode,
            new BZip2StreamOptions
            {
                DecompressConcatenated = decompressConcatenated,
                LeaveStreamOpen = leaveOpen,
                TolerateTruncatedStream = tolerateTruncatedStream,
            }
        );

    /// <summary>
    /// Create a BZip2Stream
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="compressionMode">Compression Mode</param>
    /// <param name="options">BZip2 stream options</param>
    public static BZip2Stream Create(
        Stream stream,
        CompressionMode compressionMode,
        BZip2StreamOptions options
    )
    {
        options = options ?? throw new ArgumentNullException(nameof(options));
        var bZip2Stream = new BZip2Stream();
        bZip2Stream.Mode = compressionMode;
        if (bZip2Stream.Mode == CompressionMode.Compress)
        {
            bZip2Stream.stream = new CBZip2OutputStream(stream, options);
        }
        else
        {
            bZip2Stream.stream = CBZip2InputStream.Create(stream, options);
        }

        return bZip2Stream;
    }

    public ValueTask FinishAsync() => (stream as CBZip2OutputStream)?.FinishAsync() ?? default;

    public void Finish() => (stream as CBZip2OutputStream)?.Finish();

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            base.Dispose(disposing);
            return;
        }
        isDisposed = true;
        if (disposing)
        {
            stream.Dispose();
        }
        base.Dispose(disposing);
    }

    public CompressionMode Mode { get; private set; }

    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => stream.CanSeek;

    public override bool CanWrite => stream.CanWrite;

    public override void Flush() => stream.Flush();

    public override long Length => stream.Length;

    public override long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

    public override int ReadByte() => stream.ReadByte();

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value) => stream.SetLength(value);

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer) => stream.Read(buffer);

    public override void Write(ReadOnlySpan<byte> buffer) => stream.Write(buffer);
#endif

    public override void Write(byte[] buffer, int offset, int count) =>
        stream.Write(buffer, offset, count);

    public override void WriteByte(byte value) => stream.WriteByte(value);

    /// <summary>
    /// Consumes two bytes to test if there is a BZip2 header
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static bool IsBZip2(Stream stream)
    {
        using var br = new BinaryReader(stream, Encoding.Default, leaveOpen: true);
        var chars = br.ReadBytes(2);
        if (chars.Length < 2 || chars[0] != 'B' || chars[1] != 'Z')
        {
            return false;
        }
        return true;
    }

    // Async methods moved to BZip2Stream.Async.cs
}
