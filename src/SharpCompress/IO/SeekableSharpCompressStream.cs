using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO;

internal sealed partial class SeekableSharpCompressStream : SharpCompressStream
{
    public override Stream BaseStream() => _stream;

    private readonly Stream _stream;
    private long? _recordedPosition;
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets whether to leave the underlying stream open when disposed.
    /// </summary>
    public override bool LeaveStreamOpen { get; }

    public SeekableSharpCompressStream(Stream stream, bool leaveStreamOpen = false)
        : base(Null, true, false, null)
    {
        ThrowHelper.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        LeaveStreamOpen = leaveStreamOpen;
        _stream = stream;
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    internal override bool IsRecording => _recordedPosition.HasValue;

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    public override void SetLength(long value) => _stream.SetLength(value);

    public override void Rewind(bool stopRecording = false)
    {
        if (!_recordedPosition.HasValue)
        {
            return;
        }

        _stream.Seek(_recordedPosition.Value, SeekOrigin.Begin);
        if (stopRecording)
        {
            _recordedPosition = null;
        }
    }

    public override void StartRecording(int? minBufferSize = null) =>
        _recordedPosition = _stream.Position;

    public override void StopRecording()
    {
        if (_recordedPosition.HasValue)
        {
            // Seek back to the recording anchor position, matching the behavior of the
            // non-seekable SharpCompressStream.StopRecording() which rewinds _logicalPosition.
            _stream.Seek(_recordedPosition.Value, SeekOrigin.Begin);
        }
        _recordedPosition = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        if (ThrowOnDispose)
        {
            throw new ArchiveOperationException(
                $"Attempt to dispose of a {nameof(SeekableSharpCompressStream)} when {nameof(ThrowOnDispose)} is true"
            );
        }
        _isDisposed = true;
        if (disposing && !LeaveStreamOpen)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}
