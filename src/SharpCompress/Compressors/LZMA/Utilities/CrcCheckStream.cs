using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.LZMA.Utilities;

[CLSCompliant(false)]
public class CrcCheckStream : Stream
{
    private readonly uint _crc;
    private readonly ArrayPool<long> _pool;
    private uint _mCurrentCrc = Crc.INIT_CRC;
    private bool _mClosed;

    private readonly long[] _mBytes;

    public CrcCheckStream(uint crc)
        : this(crc, ArrayPool<long>.Shared) { }

    internal CrcCheckStream(uint crc, ArrayPool<long> pool)
    {
        _crc = crc;
        _pool = pool;
        _mBytes = pool.Rent(256);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing || _mClosed)
        {
            base.Dispose(disposing);
            return;
        }

        // Claim disposal before validation, which can throw. The buffer must be returned only once.
        _mClosed = true;
        try
        {
            _mCurrentCrc = Crc.Finish(_mCurrentCrc);
            if (_mCurrentCrc != _crc)
            {
                throw new ArchiveOperationException();
            }
        }
        finally
        {
            base.Dispose(disposing);
            _pool.Return(_mBytes);
        }
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_mClosed;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new ArchiveOperationException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_mClosed)
        {
            throw new ObjectDisposedException(nameof(CrcCheckStream));
        }
        for (var i = 0; i < count; i++)
        {
            _mBytes[buffer[offset + i]]++;
        }

        _mCurrentCrc = Crc.Update(_mCurrentCrc, buffer, offset, count);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }
}
