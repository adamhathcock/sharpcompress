using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO;

internal class ListeningStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => Stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { return; }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { return; }
    }

    void IStreamStack.SetPostion(long position) { }

    private long _currentEntryTotalReadBytes;
    private readonly IExtractionListener _listener;

    public ListeningStream(IExtractionListener listener, Stream stream)
    {
        Stream = stream;
        this._listener = listener;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(ListeningStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(ListeningStream));
#endif
        if (disposing)
        {
            Stream.Dispose();
        }
        base.Dispose(disposing);
    }

    public Stream Stream { get; }

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

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = Stream.Read(buffer, offset, count);
        _currentEntryTotalReadBytes += read;
        _listener.FireCompressedBytesRead(_currentEntryTotalReadBytes, _currentEntryTotalReadBytes);
        return read;
    }

    public override int ReadByte()
    {
        var value = Stream.ReadByte();
        if (value == -1)
        {
            return -1;
        }

        ++_currentEntryTotalReadBytes;
        _listener.FireCompressedBytesRead(_currentEntryTotalReadBytes, _currentEntryTotalReadBytes);
        return value;
    }

    public override long Seek(long offset, SeekOrigin origin) => Stream.Seek(offset, origin);

    public override void SetLength(long value) => Stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        Stream.Write(buffer, offset, count);
}
