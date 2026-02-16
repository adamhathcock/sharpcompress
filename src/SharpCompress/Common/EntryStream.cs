using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common;

public partial class EntryStream : Stream
{
    private readonly IReader _reader;
    private readonly Stream _stream;
    private bool _completed;
    private bool _isDisposed;

    internal EntryStream(IReader reader, Stream stream)
    {
        _reader = reader;
        _stream = stream;
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
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        if (!(_completed || _reader.Cancelled))
        {
            if (Utility.UseSyncOverAsyncDispose())
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
#pragma warning disable CA2012
                SkipEntryAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            }
            else
            {
                SkipEntry();
            }
        }

        //Need a safe standard approach to this - it's okay for compression to overreads. Handling needs to be standardised
        if (_stream is IStreamStack ss)
        {
            if (
                ss.GetStream<SharpCompress.Compressors.Deflate.DeflateStream>()
                is SharpCompress.Compressors.Deflate.DeflateStream deflateStream
            )
            {
                deflateStream.Flush(); //Deflate over reads. Knock it back
            }
            else if (
                ss.GetStream<SharpCompress.Compressors.LZMA.LzmaStream>()
                is SharpCompress.Compressors.LZMA.LzmaStream lzmaStream
            )
            {
                lzmaStream.Flush(); //Lzma over reads. Knock it back
            }
        }
        base.Dispose(disposing);
        _stream.Dispose();
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();
}
