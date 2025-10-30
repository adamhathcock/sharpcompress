using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA.Utilites;

internal class CrcBuilderStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _mTarget;

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

    private readonly Stream _mTarget;
    private uint _mCrc;
    private bool _mFinished;
    private bool _isDisposed;

    public CrcBuilderStream(Stream target)
    {
        _mTarget = target;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(CrcBuilderStream));
#endif
        _mCrc = Crc.INIT_CRC;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(CrcBuilderStream));
#endif
        _mTarget.Dispose();
        base.Dispose(disposing);
    }

    public long Processed { get; private set; }

    public uint Finish()
    {
        if (!_mFinished)
        {
            _mFinished = true;
            _mCrc = Crc.Finish(_mCrc);
        }

        return _mCrc;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_mFinished)
        {
            throw new InvalidOperationException("CRC calculation has been finished.");
        }

        Processed += count;
        _mCrc = Crc.Update(_mCrc, buffer, offset, count);
        _mTarget.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_mFinished)
        {
            throw new InvalidOperationException("CRC calculation has been finished.");
        }

        Processed += count;
        _mCrc = Crc.Update(_mCrc, buffer, offset, count);
        await _mTarget.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
