using System;
using System.IO;

namespace SharpCompress.IO;

internal partial class RewindableStream : Stream, IStreamStack
{
    public virtual Stream BaseStream() => stream;

    private readonly Stream stream;
    private MemoryStream bufferStream = new MemoryStream();
    private bool isRewound;
    private bool isDisposed;
    private long streamPosition;

    // Rolling buffer for limited backward seeking without unbounded memory growth.
    private RingBuffer? _ringBuffer;
    private long _logicalPosition; // The current logical read position (can be behind streamPosition)

    // Passthrough mode - no buffering, delegates CanSeek to underlying stream
    private readonly bool _isPassthrough;

    /// <summary>
    /// Gets whether this stream is in passthrough mode (no buffering, delegates to underlying stream).
    /// </summary>
    internal bool IsPassthrough => _isPassthrough;

    /// <summary>
    /// Default size for rolling buffer (same as .NET Stream.CopyTo default)
    /// </summary>
    public const int DefaultRollingBufferSize = 81920;

    /// <summary>
    /// Gets or sets whether to leave the underlying stream open when disposed.
    /// </summary>
    public bool LeaveStreamOpen { get; set; }

    /// <summary>
    /// Gets or sets whether to throw an exception when Dispose is called.
    /// Useful for testing to ensure streams are not disposed prematurely.
    /// </summary>
    public bool ThrowOnDispose { get; set; }

    public RewindableStream(Stream stream)
    {
        this.stream = stream;
        _logicalPosition = 0;
    }

    /// <summary>
    /// Creates a RewindableStream with a rolling buffer that enables limited backward seeking.
    /// </summary>
    /// <param name="stream">The underlying stream to wrap.</param>
    /// <param name="rollingBufferSize">Size of the rolling buffer in bytes.</param>
    public RewindableStream(Stream stream, int rollingBufferSize)
        : this(stream)
    {
        if (rollingBufferSize > 0)
        {
            _ringBuffer = new RingBuffer(rollingBufferSize);
        }
    }

    /// <summary>
    /// Private constructor for passthrough mode.
    /// </summary>
    private RewindableStream(Stream stream, bool leaveStreamOpen, bool passthrough)
    {
        this.stream = stream;
        LeaveStreamOpen = leaveStreamOpen;
        _isPassthrough = passthrough;
        _logicalPosition = 0;
    }

    /// <summary>
    /// Creates a RewindableStream that acts as a passthrough wrapper.
    /// No buffering is performed; CanSeek delegates to the underlying stream.
    /// The underlying stream will not be disposed when this stream is disposed.
    /// </summary>
    public static RewindableStream CreateNonDisposing(Stream stream) =>
        new(stream, leaveStreamOpen: true, passthrough: true);

    internal virtual bool IsRecording { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }
        if (ThrowOnDispose)
        {
            throw new InvalidOperationException(
                $"Attempt to dispose of a {nameof(RewindableStream)} when {nameof(ThrowOnDispose)} is true"
            );
        }
        isDisposed = true;
        base.Dispose(disposing);
        if (disposing)
        {
            if (!LeaveStreamOpen)
            {
                stream.Dispose();
            }
            _ringBuffer?.Dispose();
            _ringBuffer = null;
        }
    }

    public void Rewind() => Rewind(false);

    public virtual void Rewind(bool stopRecording)
    {
        if (_isPassthrough)
        {
            throw new InvalidOperationException(
                "Rewind cannot be called on a passthrough stream. Use EnsureSeekable() first."
            );
        }
        isRewound = true;
        IsRecording = !stopRecording;
        bufferStream.Position = 0;
    }

    public virtual void StopRecording()
    {
        if (_isPassthrough)
        {
            throw new InvalidOperationException(
                "StopRecording cannot be called on a passthrough stream. Use EnsureSeekable() first."
            );
        }
        if (!IsRecording)
        {
            throw new InvalidOperationException(
                "StopRecording can only be called when recording is active."
            );
        }
        isRewound = true;
        IsRecording = false;
        bufferStream.Position = 0;
    }

    public static RewindableStream EnsureSeekable(Stream stream)
    {
        // If it's a passthrough RewindableStream, unwrap it and create proper seekable wrapper
        if (stream is RewindableStream rewindableStream)
        {
            if (rewindableStream._isPassthrough)
            {
                // Unwrap the passthrough and create appropriate wrapper
                var underlying = rewindableStream.stream;
                if (underlying.CanSeek)
                {
                    // Create SeekableRewindableStream that preserves LeaveStreamOpen
                    return new SeekableRewindableStream(underlying)
                    {
                        LeaveStreamOpen = true, // Preserve non-disposing behavior
                    };
                }
                // Non-seekable underlying stream - wrap with rolling buffer
                return new RewindableStream(underlying, DefaultRollingBufferSize)
                {
                    LeaveStreamOpen = true,
                };
            }
            // Not passthrough - return as-is
            return rewindableStream;
        }

        // Check if stream is wrapping a RewindableStream (e.g., via IStreamStack)
        if (stream is IStreamStack streamStack)
        {
            var underlying = streamStack.GetStream<RewindableStream>();
            if (underlying is not null)
            {
                return underlying;
            }
        }

        if (stream.CanSeek)
        {
            return new SeekableRewindableStream(stream);
        }

        // For non-seekable streams, create a RewindableStream with rolling buffer
        // to allow limited backward seeking (required by decompressors that over-read)
        return new RewindableStream(stream, DefaultRollingBufferSize);
    }

    public virtual void StartRecording()
    {
        if (_isPassthrough)
        {
            throw new InvalidOperationException(
                "StartRecording cannot be called on a passthrough stream. Use EnsureSeekable() first."
            );
        }
        if (IsRecording)
        {
            throw new InvalidOperationException(
                "StartRecording can only be called when not already recording."
            );
        }
        //if (isRewound && bufferStream.Position != 0)
        //   throw new System.NotImplementedException();
        if (bufferStream.Position != 0)
        {
            var data = bufferStream.ToArray();
            var position = bufferStream.Position;
            bufferStream.SetLength(0);
            bufferStream.Write(data, (int)position, data.Length - (int)position);
            bufferStream.Position = 0;
        }
        IsRecording = true;
    }

    public override bool CanRead => true;

    public override bool CanSeek => _isPassthrough ? stream.CanSeek : true;

    public override bool CanWrite => _isPassthrough && stream.CanWrite;

    public override void Flush()
    {
        if (_isPassthrough)
        {
            stream.Flush();
            return;
        }
        throw new NotSupportedException();
    }

    public override long Length =>
        _isPassthrough ? stream.Length : throw new NotSupportedException();

    public override long Position
    {
        get
        {
            // In passthrough mode, delegate to underlying stream
            if (_isPassthrough)
            {
                return stream.Position;
            }
            // If recording is active or rewound from recording, use recording buffer position
            if (IsRecording || (isRewound && bufferStream.Position < bufferStream.Length))
            {
                return streamPosition - bufferStream.Length + bufferStream.Position;
            }
            // If ring buffer is active (and not recording), use logical position
            if (_ringBuffer is not null)
            {
                return _logicalPosition;
            }
            return streamPosition;
        }
        set
        {
            // In passthrough mode, delegate to underlying stream
            if (_isPassthrough)
            {
                stream.Position = value;
                return;
            }
            SeekToPosition(value);
        }
    }

    private void SeekToPosition(long targetPosition)
    {
        // If recording is active, use recording buffer for seeking
        if (IsRecording || isRewound)
        {
            long bufferStart = streamPosition - bufferStream.Length;
            long bufferEnd = streamPosition;

            if (targetPosition >= bufferStart && targetPosition <= bufferEnd)
            {
                isRewound = true;
                bufferStream.Position = targetPosition - bufferStart;
                return;
            }
            throw new NotSupportedException("Cannot seek outside recorded region.");
        }

        // If ring buffer is enabled, check if we can seek within it
        if (_ringBuffer is not null)
        {
            long ringBufferStart = streamPosition - _ringBuffer.Length;
            if (targetPosition >= ringBufferStart && targetPosition <= streamPosition)
            {
                _logicalPosition = targetPosition;
                return;
            }
            // Can't seek outside ring buffer range
            throw new NotSupportedException(
                $"Cannot seek to position {targetPosition}. Valid range with ring buffer: [{ringBufferStart}, {streamPosition}]"
            );
        }

        // No buffering available
        throw new NotSupportedException("Cannot seek on non-buffered stream.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        // In passthrough mode, delegate directly to underlying stream
        if (_isPassthrough)
        {
            return stream.Read(buffer, offset, count);
        }

        // If recording is active or we're reading from the recording buffer, use legacy behavior
        // Recording takes precedence over rolling buffer for format detection
        if (IsRecording || (isRewound && bufferStream.Position != bufferStream.Length))
        {
            return ReadWithRecording(buffer, offset, count);
        }

        // If ring buffer is enabled (and not recording), use ring buffer logic
        if (_ringBuffer is not null)
        {
            return ReadWithRingBuffer(buffer, offset, count);
        }

        // No buffering - read directly from stream
        int read = stream.Read(buffer, offset, count);
        streamPosition += read;
        _logicalPosition = streamPosition;
        return read;
    }

    /// <summary>
    /// Reads data using the recording buffer (legacy behavior for format detection).
    /// </summary>
    private int ReadWithRecording(byte[] buffer, int offset, int count)
    {
        int read;
        if (isRewound && bufferStream.Position != bufferStream.Length)
        {
            var readCount = Math.Min(count, (int)(bufferStream.Length - bufferStream.Position));
            read = bufferStream.Read(buffer, offset, readCount);
            if (read < count)
            {
                var tempRead = stream.Read(buffer, offset + read, count - read);
                if (IsRecording)
                {
                    bufferStream.Write(buffer, offset + read, tempRead);
                }
                else if (_ringBuffer is not null && tempRead > 0)
                {
                    // When transitioning out of recording mode, add to ring buffer
                    // so that future rewinds will work
                    _ringBuffer.Write(buffer, offset + read, tempRead);
                }
                streamPosition += tempRead;
                _logicalPosition = streamPosition;
                read += tempRead;
            }
            if (bufferStream.Position == bufferStream.Length)
            {
                isRewound = false;
            }
            return read;
        }

        read = stream.Read(buffer, offset, count);
        if (IsRecording)
        {
            bufferStream.Write(buffer, offset, read);
        }
        streamPosition += read;
        _logicalPosition = streamPosition;
        return read;
    }

    /// <summary>
    /// Reads data using the ring buffer. If logical position is behind stream position,
    /// serves data from the ring buffer first.
    /// </summary>
    private int ReadWithRingBuffer(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        // If logical position is behind stream position, read from ring buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            long bytesFromEnd = streamPosition - _logicalPosition;
            int available = _ringBuffer!.ReadFromEnd(bytesFromEnd, buffer, offset, count);
            totalRead += available;
            offset += available;
            count -= available;
            _logicalPosition += available;
        }

        // If more data needed, read from underlying stream
        if (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read > 0)
            {
                _ringBuffer!.Write(buffer, offset, read);
                streamPosition += read;
                _logicalPosition += read;
                totalRead += read;
            }
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        // In passthrough mode, delegate to underlying stream
        if (_isPassthrough)
        {
            return stream.Seek(offset, origin);
        }

        long targetPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => throw new NotSupportedException("Seeking from end is not supported."),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        SeekToPosition(targetPosition);
        return targetPosition;
    }

    public override void SetLength(long value)
    {
        if (_isPassthrough)
        {
            stream.SetLength(value);
            return;
        }
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_isPassthrough)
        {
            stream.Write(buffer, offset, count);
            return;
        }
        throw new NotSupportedException();
    }

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer)
    {
        if (_isPassthrough)
        {
            return stream.Read(buffer);
        }
        // Fall back to base implementation for buffered modes
        return base.Read(buffer);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_isPassthrough)
        {
            stream.Write(buffer);
            return;
        }
        throw new NotSupportedException();
    }
#endif
}
