using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO;

/// <summary>
/// Stream wrapper that provides optional ring-buffered reading for non-seekable
/// or forward-only streams, enabling limited backward seeking required by some
/// decompressors and archive formats.
/// </summary>
/// <remarks>
/// In most cases, callers should obtain an instance via the static
/// <c>SharpCompressStream.Create(...)</c> methods rather than constructing this
/// class directly. The <c>Create</c> methods select an appropriate configuration
/// (such as passthrough vs buffered mode and buffer size) for the underlying
/// stream and usage scenario.
/// </remarks>
public partial class SharpCompressStream : Stream, IStreamStack
{
    public virtual Stream BaseStream() => stream;

    private readonly Stream stream;
    private bool isDisposed;
    private long streamPosition;

    // Ring buffer for recording mode and over-read protection.
    // Single unified buffering mechanism for both use cases.
    private RingBuffer? _ringBuffer;
    private long _logicalPosition; // The current logical read position (can be behind streamPosition)

    // Recording state: anchor position when StartRecording was called
    private long? _recordingStartPosition;
    private bool _isRecording;

    // Passthrough mode - no buffering, delegates CanSeek to underlying stream
    private readonly bool _isPassthrough;

    /// <summary>
    /// Gets whether this stream is in passthrough mode (no buffering, delegates to underlying stream).
    /// </summary>
    internal bool IsPassthrough => _isPassthrough;

    /// <summary>
    /// Gets whether to leave the underlying stream open when disposed.
    /// </summary>
    public virtual bool LeaveStreamOpen { get; }

    /// <summary>
    /// Gets or sets whether to throw an exception when Dispose is called.
    /// Useful for testing to ensure streams are not disposed prematurely.
    /// </summary>
    public virtual bool ThrowOnDispose { get; set; }

    public SharpCompressStream(Stream stream)
    {
        this.stream = stream;
        _logicalPosition = 0;
    }

    /// <summary>
    /// Private constructor for passthrough mode.
    /// </summary>
    protected SharpCompressStream(
        Stream stream,
        bool leaveStreamOpen,
        bool passthrough,
        int? bufferSize
    )
    {
        this.stream = stream;
        LeaveStreamOpen = leaveStreamOpen;
        _isPassthrough = passthrough;
        _logicalPosition = 0;

        if (bufferSize.HasValue && bufferSize.Value > 0)
        {
            _ringBuffer = new RingBuffer(bufferSize.Value);
        }
    }

    /// <summary>
    /// Gets whether the stream is actively recording reads to the ring buffer.
    /// </summary>
    internal virtual bool IsRecording => _isRecording;

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }
        if (ThrowOnDispose)
        {
            throw new ArchiveOperationException(
                $"Attempt to dispose of a {nameof(SharpCompressStream)} when {nameof(ThrowOnDispose)} is true"
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
            throw new ArchiveOperationException(
                "Rewind cannot be called on a passthrough stream. Use Create() first."
            );
        }

        if (_recordingStartPosition is null)
        {
            throw new ArchiveOperationException(
                "Rewind can only be called after StartRecording() has been called."
            );
        }

        // Verify recording anchor is within ring buffer range
        long anchorAge = streamPosition - _recordingStartPosition.Value;
        if (anchorAge > _ringBuffer!.Length)
        {
            throw new ArchiveOperationException(
                $"Cannot rewind: recording anchor is {anchorAge} bytes behind current position, "
                    + $"but ring buffer only holds {_ringBuffer.Length} bytes. "
                    + $"Recording buffer overflow - increase DefaultRollingBufferSize or reduce format detection reads."
            );
        }

        // Rewind logical position to recording anchor
        _logicalPosition = _recordingStartPosition.Value;

        if (stopRecording)
        {
            _isRecording = false;
            // Note: We keep _recordingStartPosition so Rewind() can be called again
            // (frozen recording mode). The anchor is only cleared when a new recording
            // starts or the stream is disposed.
        }
    }

    public virtual void StopRecording()
    {
        if (_isPassthrough)
        {
            throw new ArchiveOperationException(
                "StopRecording cannot be called on a passthrough stream. Use Create() first."
            );
        }
        if (!IsRecording)
        {
            throw new ArchiveOperationException(
                "StopRecording can only be called when recording is active."
            );
        }

        // Mark that we're no longer actively recording
        _isRecording = false;

        // Rewind to recording anchor position
        _logicalPosition = _recordingStartPosition!.Value;

        // Note: We keep _recordingStartPosition so future Rewind() calls still work
        // (frozen recording mode) until Rewind(stopRecording: true) is called
    }

    public virtual void StartRecording()
    {
        if (_isPassthrough)
        {
            throw new ArchiveOperationException(
                "StartRecording cannot be called on a passthrough stream. Use Create() first."
            );
        }
        if (IsRecording)
        {
            throw new ArchiveOperationException(
                "StartRecording can only be called when not already recording."
            );
        }

        // Ensure ring buffer exists
        if (_ringBuffer is null)
        {
            _ringBuffer = new RingBuffer(Constants.BufferSize);
        }

        // Mark current position as recording anchor
        _recordingStartPosition = streamPosition;
        _logicalPosition = streamPosition;
        _isRecording = true;
    }

    public override bool CanRead => true;

    public override bool CanSeek => !_isPassthrough || stream.CanSeek;

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

    public override long Length
    {
        get
        {
            if (_isPassthrough)
            {
                return stream.Length;
            }

            if (_ringBuffer is not null)
            {
                return _ringBuffer.Length;
            }
            throw new NotSupportedException();
        }
    }

    public override long Position
    {
        get
        {
            // In passthrough mode, delegate to underlying stream
            if (_isPassthrough)
            {
                return stream.Position;
            }
            // Use logical position (same for both recording and ring buffer modes)
            return _logicalPosition;
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
        // If we have a recording anchor, allow seeking within the recorded range
        if (_recordingStartPosition is not null)
        {
            if (targetPosition >= _recordingStartPosition.Value && targetPosition <= streamPosition)
            {
                _logicalPosition = targetPosition;
                return;
            }
            throw new NotSupportedException(
                $"Cannot seek to position {targetPosition}. Valid recorded range: "
                    + $"[{_recordingStartPosition.Value}, {streamPosition}]"
            );
        }

        // If ring buffer is enabled (and not recording), check if we can seek within it
        if (_ringBuffer is not null)
        {
            long ringBufferStart = streamPosition - _ringBuffer.Length;
            if (targetPosition >= ringBufferStart && targetPosition <= streamPosition)
            {
                _logicalPosition = targetPosition;
                return;
            }
            throw new NotSupportedException(
                $"Cannot seek to position {targetPosition}. Valid ring buffer range: "
                    + $"[{ringBufferStart}, {streamPosition}]"
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

        // If ring buffer exists, use unified buffered read logic
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
    /// Reads data using the ring buffer. If logical position is behind stream position,
    /// serves data from the ring buffer first. Handles both recording mode and
    /// over-read protection uniformly.
    /// </summary>
    private int ReadWithRingBuffer(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        // If logical position is behind stream position, read from ring buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            long bytesFromEnd = streamPosition - _logicalPosition;

            // Verify data is available in ring buffer
            if (!_ringBuffer!.CanReadFromEnd(bytesFromEnd))
            {
                throw new ArchiveOperationException(
                    $"Ring buffer underflow: trying to read {bytesFromEnd} bytes back, "
                        + $"but buffer only holds {_ringBuffer.Length} bytes."
                );
            }

            int available = _ringBuffer.ReadFromEnd(bytesFromEnd, buffer, offset, count);
            totalRead += available;
            offset += available;
            count -= available;
            _logicalPosition += available;
        }

        // If more data needed and we're caught up, read from underlying stream
        if (count > 0 && _logicalPosition == streamPosition)
        {
            // Use async read if stream doesn't support sync reads (e.g., AsyncOnlyStream)
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
