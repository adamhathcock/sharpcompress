using System;
using System.Buffers;
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
    // This is a circular buffer that keeps the last N bytes read from the stream.
    private byte[]? _rollingBuffer;
    private int _rollingBufferSize;
    private int _rollingBufferWritePos; // Next write position in circular buffer
    private int _rollingBufferLength; // Number of valid bytes in rolling buffer (0 to _rollingBufferSize)
    private long _logicalPosition; // The current logical read position (can be behind streamPosition)

    /// <summary>
    /// Default size for rolling buffer (same as .NET Stream.CopyTo default)
    /// </summary>
    public const int DefaultRollingBufferSize = 81920;

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
            _rollingBuffer = ArrayPool<byte>.Shared.Rent(rollingBufferSize);
            _rollingBufferSize = rollingBufferSize;
            _rollingBufferWritePos = 0;
            _rollingBufferLength = 0;
        }
    }

    internal virtual bool IsRecording { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
        base.Dispose(disposing);
        if (disposing)
        {
            stream.Dispose();
            if (_rollingBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_rollingBuffer);
                _rollingBuffer = null;
            }
        }
    }

    public void Rewind() => Rewind(false);

    public virtual void Rewind(bool stopRecording)
    {
        isRewound = true;
        IsRecording = !stopRecording;
        bufferStream.Position = 0;
    }

    public virtual void StopRecording()
    {
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
        if (stream is RewindableStream rewindableStream)
        {
            return rewindableStream;
        }

        // Check if stream is wrapping a RewindableStream (e.g., NonDisposingStream)
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

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override void Flush() => throw new NotSupportedException();

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get
        {
            // If recording is active or rewound from recording, use recording buffer position
            if (IsRecording || (isRewound && bufferStream.Position < bufferStream.Length))
            {
                return streamPosition - bufferStream.Length + bufferStream.Position;
            }
            // If rolling buffer is active (and not recording), use logical position
            if (_rollingBuffer is not null)
            {
                return _logicalPosition;
            }
            return streamPosition;
        }
        set => SeekToPosition(value);
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

        // If rolling buffer is enabled, check if we can seek within it
        if (_rollingBuffer is not null)
        {
            long rollingBufferStart = streamPosition - _rollingBufferLength;
            if (targetPosition >= rollingBufferStart && targetPosition <= streamPosition)
            {
                _logicalPosition = targetPosition;
                return;
            }
            // Can't seek outside rolling buffer range
            throw new NotSupportedException(
                $"Cannot seek to position {targetPosition}. Valid range with rolling buffer: [{rollingBufferStart}, {streamPosition}]"
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

        // If recording is active or we're reading from the recording buffer, use legacy behavior
        // Recording takes precedence over rolling buffer for format detection
        if (IsRecording || (isRewound && bufferStream.Position != bufferStream.Length))
        {
            return ReadWithRecording(buffer, offset, count);
        }

        // If rolling buffer is enabled (and not recording), use rolling buffer logic
        if (_rollingBuffer is not null)
        {
            return ReadWithRollingBuffer(buffer, offset, count);
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
                else if (_rollingBuffer is not null && tempRead > 0)
                {
                    // When transitioning out of recording mode, add to rolling buffer
                    // so that future rewinds will work
                    AddToRollingBuffer(buffer, offset + read, tempRead);
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
    /// Reads data using the rolling buffer. If logical position is behind stream position,
    /// serves data from the rolling buffer first.
    /// </summary>
    private int ReadWithRollingBuffer(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        // If logical position is behind stream position, read from rolling buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            // Calculate offset in rolling buffer
            long bytesFromEnd = streamPosition - _logicalPosition;
            if (bytesFromEnd > _rollingBufferLength)
            {
                // This shouldn't happen if SeekToPosition validated correctly
                throw new InvalidOperationException(
                    "Logical position is outside rolling buffer range."
                );
            }

            // Find the index in the circular buffer
            // _rollingBufferWritePos is where next byte would be written (one past last valid byte)
            // So the byte at _logicalPosition is at: (_rollingBufferWritePos - bytesFromEnd + _rollingBufferSize) % _rollingBufferSize
            int bufferIndex = (int)(
                (_rollingBufferWritePos - bytesFromEnd + _rollingBufferSize) % _rollingBufferSize
            );
            int availableFromBuffer = (int)Math.Min(bytesFromEnd, count);

            // Read from rolling buffer (may wrap around)
            int firstPart = Math.Min(availableFromBuffer, _rollingBufferSize - bufferIndex);
            Array.Copy(_rollingBuffer!, bufferIndex, buffer, offset, firstPart);
            if (firstPart < availableFromBuffer)
            {
                // Wrap around
                Array.Copy(
                    _rollingBuffer!,
                    0,
                    buffer,
                    offset + firstPart,
                    availableFromBuffer - firstPart
                );
            }

            totalRead += availableFromBuffer;
            offset += availableFromBuffer;
            count -= availableFromBuffer;
            _logicalPosition += availableFromBuffer;
        }

        // If more data needed, read from underlying stream
        if (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read > 0)
            {
                // Add to rolling buffer
                AddToRollingBuffer(buffer, offset, read);
                streamPosition += read;
                _logicalPosition += read;
                totalRead += read;
            }
        }

        return totalRead;
    }

    /// <summary>
    /// Adds data to the rolling buffer (circular).
    /// </summary>
    private void AddToRollingBuffer(byte[] data, int offset, int count)
    {
        if (_rollingBuffer is null || count == 0)
        {
            return;
        }

        // If data is larger than buffer, only keep the last _rollingBufferSize bytes
        if (count >= _rollingBufferSize)
        {
            Array.Copy(
                data,
                offset + count - _rollingBufferSize,
                _rollingBuffer,
                0,
                _rollingBufferSize
            );
            _rollingBufferWritePos = 0;
            _rollingBufferLength = _rollingBufferSize;
            return;
        }

        // Write data to circular buffer
        int firstPart = Math.Min(count, _rollingBufferSize - _rollingBufferWritePos);
        Array.Copy(data, offset, _rollingBuffer, _rollingBufferWritePos, firstPart);
        if (firstPart < count)
        {
            // Wrap around
            Array.Copy(data, offset + firstPart, _rollingBuffer, 0, count - firstPart);
        }

        _rollingBufferWritePos = (_rollingBufferWritePos + count) % _rollingBufferSize;
        _rollingBufferLength = Math.Min(_rollingBufferLength + count, _rollingBufferSize);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
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

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
