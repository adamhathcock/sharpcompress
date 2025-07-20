using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

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
                    _buffer = new byte[_bufferSize];
                    _bufferPosition = 0;
                    _bufferedLength = 0;
                    if (_bufferingEnabled)
                    {
                        ValidateBufferState(); // Add here
                    }
                    try
                    {
                        _internalPosition = Stream.Position;
                    }
                    catch
                    {
                        _internalPosition = 0;
                    }
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
                _internalPosition = value;
                _bufferPosition = value;
                ValidateBufferState(); // Add here
            }
        }
    }

    void IStreamStack.SetPostion(long position) { }

    public Stream Stream { get; }

    //private MemoryStream _bufferStream = new();

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
        try
        {
            _baseInitialPos = stream.Position;
        }
        catch
        {
            _baseInitialPos = 0;
        }

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(SharpCompressStream));
#endif
    }

    internal bool IsRecording { get; private set; }

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

        if (targetPos >= bufferPos && targetPos < bufferPos + _bufferedLength)
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

#if !NETFRAMEWORK && !NETSTANDARD2_0

    //public override int Read(Span<byte> buffer)
    //{
    //    int bytesRead = Stream.Read(buffer);
    //    _internalPosition += bytesRead;
    //    return bytesRead;
    //}

    //    public override void Write(ReadOnlySpan<byte> buffer)
    //    {
    //        Stream.Write(buffer);
    //        _internalPosition += buffer.Length;
    //    }

#endif
}
