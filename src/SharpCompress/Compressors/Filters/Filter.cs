using System;
using System.IO;

namespace SharpCompress.Compressors.Filters;

internal abstract partial class Filter : Stream
{
    protected bool _isEncoder;
    protected Stream _baseStream;

    private readonly byte[] _tail;
    private readonly byte[] _window;
    private int _transformed;
    private int _read;
    private bool _endReached;
    private bool _isDisposed;

    protected Filter(bool isEncoder, Stream baseStream, int lookahead)
    {
        _isEncoder = isEncoder;
        _baseStream = baseStream;
        _tail = new byte[lookahead - 1];
        _window = new byte[_tail.Length * 2];
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        base.Dispose(disposing);
        _baseStream.Dispose();
    }

    public override bool CanRead => !_isEncoder;

    public override bool CanSeek => false;

    public override bool CanWrite => _isEncoder;

    public override void Flush() { }

    public override long Length => _baseStream.Length;

    public override long Position
    {
        get => _baseStream.Position;
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected abstract int Transform(byte[] buffer, int offset, int count);
}
