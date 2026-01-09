using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

public class SharpCompressStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => Stream;

    // Buffering fields
    private int _bufferSize;
    private byte[]? _buffer;
    private int _bufferPosition;
    private int _bufferedLength;
    private bool _bufferingEnabled;
    private long _baseInitialPos;

    private void ValidateBufferState()
    {
        if (_bufferPosition < 0 || _bufferPosition > _bufferedLength)
        {
            throw new InvalidOperationException(
                "Buffer state is inconsistent: _bufferPosition is out of range."
            );
        }
    }

    int IStreamStack.BufferSize
    {
        get => _bufferingEnabled ? _bufferSize : 0;
        set //need to adjust an already existing buffer
        {
            if (_bufferSize != value)
            {
                _bufferSize = value;
                _bufferingEnabled = _bufferSize > 0;
                if (_bufferingEnabled)
                {
                    if (_buffer is not null)
                    {
                        ArrayPool<byte>.Shared.Return(_buffer);
                    }
                    _buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
                    _bufferPosition = 0;
                    _bufferedLength = 0;
                    if (_bufferingEnabled)
                    {
                        ValidateBufferState(); // Add here
                    }
                    // Check CanSeek before accessing Position to avoid exception overhead on non-seekable streams.
                    _internalPosition = Stream.CanSeek ? Stream.Position : 0;
                }
            }
        }
    }

    int IStreamStack.BufferPosition
    {
        get => _bufferingEnabled ? _bufferPosition : 0;
        set
        {
            if (_bufferingEnabled)
            {
                if (value < 0 || value > _bufferedLength)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _internalPosition = _internalPosition - _bufferPosition + value;
                _bufferPosition = value;
                ValidateBufferState(); // Add here
            }
        }
    }

    void IStreamStack.SetPosition(long position) { }

    public Stream Stream { get; }

    private bool _readOnly; //some archive detection requires seek to be disabled to cause it to exception to try the next arc type

    //private bool _isRewound;
    private bool _isDisposed;
    private long _internalPosition = 0;

    public bool ThrowOnDispose { get; set; }
    public bool LeaveOpen { get; set; }

    public long InternalPosition => _internalPosition;

    public static SharpCompressStream Create(
        Stream stream,
        bool leaveOpen = false,
        bool throwOnDispose = false,
        int bufferSize = 0,
        bool forceBuffer = false
    )
    {
        if (
            stream is SharpCompressStream sc
            && sc.LeaveOpen == leaveOpen
            && sc.ThrowOnDispose == throwOnDispose
        )
        {
            if (bufferSize != 0)
                ((IStreamStack)stream).SetBuffer(bufferSize, forceBuffer);
            return sc;
        }
        return new SharpCompressStream(stream, leaveOpen, throwOnDispose, bufferSize, forceBuffer);
    }

    public SharpCompressStream(
        Stream stream,
        bool leaveOpen = false,
        bool throwOnDispose = false,
        int bufferSize = 0,
        bool forceBuffer = false
    )
    {
        Stream = stream;
        this.LeaveOpen = leaveOpen;
        this.ThrowOnDispose = throwOnDispose;
        _readOnly = !Stream.CanSeek;

        ((IStreamStack)this).SetBuffer(bufferSize, forceBuffer);
        // Check CanSeek before accessing Position to avoid exception overhead on non-seekable streams.
        _baseInitialPos = Stream.CanSeek ? Stream.Position : 0;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(SharpCompressStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(SharpCompressStream));
#endif
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        base.Dispose(disposing);

        if (this.LeaveOpen)
        {
            return;
        }
        if (ThrowOnDispose)
        {
            throw new InvalidOperationException(
                $"Attempt to dispose of a {nameof(SharpCompressStream)} when {nameof(ThrowOnDispose)} is {ThrowOnDispose}"
            );
        }
        if (disposing)
        {
            Stream.Dispose();
            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }
        }
    }

    public override bool CanRead => Stream.CanRead;

    public override bool CanSeek => !_readOnly && Stream.CanSeek;

    public override bool CanWrite => !_readOnly && Stream.CanWrite;

    public override void Flush()
    {
        Stream.Flush();
    }

    public override long Length
    {
        get { return Stream.Length; }
    }

    public override long Position
    {
        get
        {
            long pos = _internalPosition; // Stream.Position + _bufferStream.Position - _bufferStream.Length;
            return pos;
        }
        set { Seek(value, SeekOrigin.Begin); }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
            return 0;

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed
            if (_bufferedLength == 0)
            {
                _bufferedLength = Stream.Read(_buffer!, 0, _bufferSize);
                _bufferPosition = 0;
            }
            int available = _bufferedLength - _bufferPosition;
            int toRead = Math.Min(count, available);
            if (toRead > 0)
            {
                Array.Copy(_buffer!, _bufferPosition, buffer, offset, toRead);
                _bufferPosition += toRead;
                _internalPosition += toRead;
                return toRead;
            }
            // If buffer exhausted, refill
            int r = Stream.Read(_buffer!, 0, _bufferSize);
            if (r == 0)
                return 0;
            _bufferedLength = r;
            _bufferPosition = 0;
            if (_bufferedLength == 0)
            {
                return 0;
            }
            toRead = Math.Min(count, _bufferedLength);
            Array.Copy(_buffer!, 0, buffer, offset, toRead);
            _bufferPosition = toRead;
            _internalPosition += toRead;
            return toRead;
        }
        else
        {
            if (count == 0)
            {
                return 0;
            }
            int read;
            read = Stream.Read(buffer, offset, count);
            _internalPosition += read;
            return read;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_bufferingEnabled)
        {
            ValidateBufferState();
        }

        long orig = _internalPosition;
        long targetPos;
        // Calculate the absolute target position based on origin
        switch (origin)
        {
            case SeekOrigin.Begin:
                targetPos = offset;
                break;
            case SeekOrigin.Current:
                targetPos = _internalPosition + offset;
                break;
            case SeekOrigin.End:
                targetPos = this.Length + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        long bufferPos = _internalPosition - _bufferPosition;

        if (targetPos >= bufferPos && targetPos <= bufferPos + _bufferedLength)
        {
            _bufferPosition = (int)(targetPos - bufferPos); //repoint within the buffer
            _internalPosition = targetPos;
        }
        else
        {
            long newStreamPos =
                Stream.Seek(targetPos + _baseInitialPos, SeekOrigin.Begin) - _baseInitialPos;
            _internalPosition = newStreamPos;
            _bufferPosition = 0;
            _bufferedLength = 0;
        }

        return _internalPosition;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void WriteByte(byte value)
    {
        Stream.WriteByte(value);
        ++_internalPosition;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Stream.Write(buffer, offset, count);
        _internalPosition += count;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count == 0)
            return 0;

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed
            if (_bufferedLength == 0)
            {
                _bufferedLength = await Stream
                    .ReadAsync(_buffer!, 0, _bufferSize, cancellationToken)
                    .ConfigureAwait(false);
                _bufferPosition = 0;
            }
            int available = _bufferedLength - _bufferPosition;
            int toRead = Math.Min(count, available);
            if (toRead > 0)
            {
                Array.Copy(_buffer!, _bufferPosition, buffer, offset, toRead);
                _bufferPosition += toRead;
                _internalPosition += toRead;
                return toRead;
            }
            // If buffer exhausted, refill
            int r = await Stream
                .ReadAsync(_buffer!, 0, _bufferSize, cancellationToken)
                .ConfigureAwait(false);
            if (r == 0)
                return 0;
            _bufferedLength = r;
            _bufferPosition = 0;
            if (_bufferedLength == 0)
            {
                return 0;
            }
            toRead = Math.Min(count, _bufferedLength);
            Array.Copy(_buffer!, 0, buffer, offset, toRead);
            _bufferPosition = toRead;
            _internalPosition += toRead;
            return toRead;
        }
        else
        {
            int read = await Stream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            _internalPosition += read;
            return read;
        }
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        await Stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _internalPosition += count;
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.Length == 0)
            return 0;

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed
            if (_bufferedLength == 0)
            {
                _bufferedLength = await Stream
                    .ReadAsync(_buffer.AsMemory(0, _bufferSize), cancellationToken)
                    .ConfigureAwait(false);
                _bufferPosition = 0;
            }
            int available = _bufferedLength - _bufferPosition;
            int toRead = Math.Min(buffer.Length, available);
            if (toRead > 0)
            {
                _buffer.AsSpan(_bufferPosition, toRead).CopyTo(buffer.Span);
                _bufferPosition += toRead;
                _internalPosition += toRead;
                return toRead;
            }
            // If buffer exhausted, refill
            int r = await Stream
                .ReadAsync(_buffer.AsMemory(0, _bufferSize), cancellationToken)
                .ConfigureAwait(false);
            if (r == 0)
                return 0;
            _bufferedLength = r;
            _bufferPosition = 0;
            if (_bufferedLength == 0)
            {
                return 0;
            }
            toRead = Math.Min(buffer.Length, _bufferedLength);
            _buffer.AsSpan(0, toRead).CopyTo(buffer.Span);
            _bufferPosition = toRead;
            _internalPosition += toRead;
            return toRead;
        }
        else
        {
            int read = await Stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _internalPosition += read;
            return read;
        }
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await Stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _internalPosition += buffer.Length;
    }

#endif
}
