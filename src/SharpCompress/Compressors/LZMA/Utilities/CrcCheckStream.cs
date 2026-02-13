using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.Utilities;

[CLSCompliant(false)]
public class CrcCheckStream(uint crc) : Stream
{
    private uint _mCurrentCrc = Crc.INIT_CRC;
    private bool _mClosed;

    private readonly long[] _mBytes = ArrayPool<long>.Shared.Rent(256);

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && !_mClosed)
            {
                _mClosed = true;
                _mCurrentCrc = Crc.Finish(_mCurrentCrc); //now becomes equal

                if (_mCurrentCrc != crc) //moved test to here
                {
                    throw new InvalidOperationException();
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
            ArrayPool<long>.Shared.Return(_mBytes);
        }
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
