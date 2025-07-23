using System;
using System.IO;
using System.IO.Compression;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common;

public class EntryStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private readonly IReader _reader;
    private readonly Stream _stream;
    private bool _completed;
    private bool _isDisposed;

    internal EntryStream(IReader reader, Stream stream)
    {
        _reader = reader;
        _stream = stream;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(EntryStream));
#endif
    }

    /// <summary>
    /// When reading a stream from OpenEntryStream, the stream must be completed so use this to finish reading the entire entry.
    /// </summary>
    public void SkipEntry()
    {
        this.Skip();
        _completed = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (!(_completed || _reader.Cancelled))
        {
            SkipEntry();
        }

        //Need a safe standard approach to this - it's okay for compression to overreads. Handling needs to be standardised
        if (_stream is IStreamStack ss)
        {
            if (ss.BaseStream() is SharpCompress.Compressors.Deflate.DeflateStream deflateStream)
            {
                deflateStream.Flush(); //Deflate over reads. Knock it back
            }
            else if (ss.BaseStream() is SharpCompress.Compressors.LZMA.LzmaStream lzmaStream)
            {
                lzmaStream.Flush(); //Lzma over reads. Knock it back
            }
        }

        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(EntryStream));
#endif
        base.Dispose(disposing);
        _stream.Dispose();
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position; //throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _stream.Read(buffer, offset, count);
        if (read <= 0)
        {
            _completed = true;
        }
        return read;
    }

    public override int ReadByte()
    {
        var value = _stream.ReadByte();
        if (value == -1)
        {
            _completed = true;
        }
        return value;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
