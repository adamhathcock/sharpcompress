using System;
using System.IO;

namespace SharpCompress.IO;

public class NonDisposingStream : Stream
{
    public static NonDisposingStream Create(Stream stream, bool throwOnDispose = false)
    {
        if (
            stream is NonDisposingStream nonDisposingStream
            && nonDisposingStream.ThrowOnDispose == throwOnDispose
        )
        {
            return nonDisposingStream;
        }
        return new NonDisposingStream(stream, throwOnDispose);
    }

    protected NonDisposingStream(Stream stream, bool throwOnDispose = false)
    {
        Stream = stream;
        ThrowOnDispose = throwOnDispose;
    }

    public bool ThrowOnDispose { get; set; }

    protected override void Dispose(bool disposing)
    {
        if (ThrowOnDispose)
        {
            throw new InvalidOperationException(
                $"Attempt to dispose of a {nameof(NonDisposingStream)} when {nameof(ThrowOnDispose)} is {ThrowOnDispose}"
            );
        }
    }

    protected Stream Stream { get; }

    public override bool CanRead => Stream.CanRead;

    public override bool CanSeek => Stream.CanSeek;

    public override bool CanWrite => Stream.CanWrite;

    public override void Flush() => Stream.Flush();

    public override long Length => Stream.Length;

    public override long Position
    {
        get => Stream.Position;
        set => Stream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => Stream.Seek(offset, origin);

    public override void SetLength(long value) => Stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        Stream.Write(buffer, offset, count);

#if !NETFRAMEWORK && !NETSTANDARD2_0

    public override int Read(Span<byte> buffer) => Stream.Read(buffer);

    public override void Write(ReadOnlySpan<byte> buffer) => Stream.Write(buffer);

#endif
}
