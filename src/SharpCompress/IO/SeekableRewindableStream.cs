using System;
using System.IO;

namespace SharpCompress.IO;

internal sealed partial class SeekableRewindableStream : RewindableStream
{
    private readonly Stream _underlyingStream;
        private long? _recordedPosition;

    public SeekableRewindableStream(Stream stream)
        : base(new NullStream())
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }
        _underlyingStream = stream;
    }

    public override bool CanRead => _underlyingStream.CanRead;

    public override bool CanSeek => _underlyingStream.CanSeek;

    public override bool CanWrite => _underlyingStream.CanWrite;

    public override long Length => _underlyingStream.Length;

    public override long Position
    {
        get => _underlyingStream.Position;
        set => _underlyingStream.Position = value;
    }

    internal override bool IsRecording => _recordedPosition.HasValue;

    public override void Flush() => _underlyingStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        _underlyingStream.Read(buffer, offset, count);

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer) => _underlyingStream.Read(buffer);
#endif

    public override long Seek(long offset, SeekOrigin origin) =>
        _underlyingStream.Seek(offset, origin);

    public override void SetLength(long value) => _underlyingStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        _underlyingStream.Write(buffer, offset, count);

#if !LEGACY_DOTNET
    public override void Write(ReadOnlySpan<byte> buffer) => _underlyingStream.Write(buffer);
#endif

    public override void Rewind(bool stopRecording = false)
    {
        if (!_recordedPosition.HasValue)
        {
            return;
        }

        _underlyingStream.Seek(_recordedPosition.Value, SeekOrigin.Begin);
        if (stopRecording)
        {
            _recordedPosition = null;
        }
    }

    public override void StartRecording()
    {
        _recordedPosition = _underlyingStream.Position;
    }

    public override void StopRecording()
    {
        _recordedPosition = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _underlyingStream.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class NullStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => 0;

#if !LEGACY_DOTNET
        public override int Read(Span<byte> buffer) => 0;
#endif

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

#if !LEGACY_DOTNET
        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
#endif
    }
}
