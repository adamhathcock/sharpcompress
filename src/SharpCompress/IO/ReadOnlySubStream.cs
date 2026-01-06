using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal class ReadOnlySubStream : SharpCompressStream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif

    Stream IStreamStack.BaseStream() => base.Stream;

    private long _position;

    public ReadOnlySubStream(Stream stream, long bytesToRead, bool leaveOpen = true)
        : this(stream, null, bytesToRead, leaveOpen) { }

    public ReadOnlySubStream(Stream stream, long? origin, long bytesToRead, bool leaveOpen = true)
        : base(stream, leaveOpen, throwOnDispose: false)
    {
        if (origin != null && stream.Position != origin.Value)
        {
            stream.Position = origin.Value;
        }
        BytesLeftToRead = bytesToRead;
        _position = 0;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(ReadOnlySubStream));
#endif
    }

    private long BytesLeftToRead { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => base.Length;

    public override long Position
    {
        get => _position; //allow position to be read (XZ uses this to calculate alignment)
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = Stream.Read(buffer, offset, count);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _position += read;
        }
        return read;
    }

    public override int ReadByte()
    {
        if (BytesLeftToRead <= 0)
        {
            return -1;
        }
        var value = Stream.ReadByte();
        if (value != -1)
        {
            --BytesLeftToRead;
            _position++;
        }
        return value;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override int Read(Span<byte> buffer)
    {
        var sliceLen = BytesLeftToRead < buffer.Length ? BytesLeftToRead : buffer.Length;
        var read = Stream.Read(buffer.Slice(0, (int)sliceLen));
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _position += read;
        }
        return read;
    }
#endif

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = await Stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _position += read;
        }
        return read;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var sliceLen = BytesLeftToRead < buffer.Length ? BytesLeftToRead : buffer.Length;
        var read = await Stream
            .ReadAsync(buffer.Slice(0, (int)sliceLen), cancellationToken)
            .ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _position += read;
        }
        return read;
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(ReadOnlySubStream));
#endif
        base.Dispose(disposing);
    }
}
