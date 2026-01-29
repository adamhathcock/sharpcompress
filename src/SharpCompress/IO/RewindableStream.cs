using System;
using System.IO;

namespace SharpCompress.IO;

internal partial class RewindableStream : Stream
{
    private readonly Stream stream;
    private MemoryStream bufferStream = new MemoryStream();
    private bool isRewound;
    private bool isDisposed;
    private long streamPosition;
    private bool _hasStoppedRecording;

    public RewindableStream(Stream stream) => this.stream = stream;

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
        _hasStoppedRecording = true;
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
        if (stream.CanSeek)
        {
            return new SeekableRewindableStream(stream);
        }
        return new RewindableStream(stream);
    }

    public virtual void StartRecording()
    {
        if (IsRecording)
        {
            throw new InvalidOperationException(
                "StartRecording can only be called when not already recording."
            );
        }
        if (_hasStoppedRecording)
        {
            throw new InvalidOperationException(
                "StartRecording cannot be called after StopRecording has been called."
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
            if (isRewound || bufferStream.Position < bufferStream.Length)
            {
                return streamPosition - bufferStream.Length + bufferStream.Position;
            }
            return streamPosition;
        }
        set
        {
            long bufferStart = streamPosition - bufferStream.Length;
            long bufferEnd = streamPosition;

            if (value >= bufferStart && value < bufferEnd)
            {
                isRewound = true;
                bufferStream.Position = value - bufferStart;
            }
            else
            {
                throw new NotSupportedException("Cannot seek outside buffered region.");
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return 0;
        }
        int read;
        if (isRewound && bufferStream.Position != bufferStream.Length)
        {
            var readCount = Math.Min(count, (int)(bufferStream.Length - bufferStream.Position));
            read = bufferStream.Read(buffer, offset, readCount);
            if (read < readCount)
            {
                var tempRead = stream.Read(buffer, offset + read, count - read);
                if (IsRecording)
                {
                    bufferStream.Write(buffer, offset + read, tempRead);
                }
                streamPosition += tempRead;
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
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
