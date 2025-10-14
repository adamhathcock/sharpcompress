using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Compressors.BZip2;

public sealed class BZip2Stream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private readonly Stream stream;
    private bool isDisposed;

    /// <summary>
    /// Create a BZip2Stream
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="compressionMode">Compression Mode</param>
    /// <param name="decompressConcatenated">Decompress Concatenated</param>
    public BZip2Stream(Stream stream, CompressionMode compressionMode, bool decompressConcatenated)
    {
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(BZip2Stream));
#endif
        Mode = compressionMode;
        if (Mode == CompressionMode.Compress)
        {
            this.stream = new CBZip2OutputStream(stream);
        }
        else
        {
            this.stream = new CBZip2InputStream(stream, decompressConcatenated);
        }
    }

    public void Finish() => (stream as CBZip2OutputStream)?.Finish();

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(BZip2Stream));
#endif
        if (disposing)
        {
            stream.Dispose();
        }
    }

    public CompressionMode Mode { get; }

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

#if !NETFRAMEWORK&& !NETSTANDARD2_0

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
        var br = new BinaryReader(stream);
        var chars = br.ReadBytes(2);
        if (chars.Length < 2 || chars[0] != 'B' || chars[1] != 'Z')
        {
            return false;
        }
        return true;
    }
}
