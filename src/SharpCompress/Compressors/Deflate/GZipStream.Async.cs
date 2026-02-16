using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate;

public partial class GZipStream
{
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        await BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        var n = await BaseStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);

        if (!_firstReadDone)
        {
            _firstReadDone = true;
            FileName = BaseStream._GzipFileName;
            Comment = BaseStream._GzipComment;
            LastModified = BaseStream._GzipMtime;
        }
        return n;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        var n = await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (!_firstReadDone)
        {
            _firstReadDone = true;
            FileName = BaseStream._GzipFileName;
            Comment = BaseStream._GzipComment;
            LastModified = BaseStream._GzipMtime;
        }
        return n;
    }
#endif

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        if (BaseStream._streamMode == ZlibBaseStream.StreamMode.Undefined)
        {
            if (BaseStream._wantCompress)
            {
                // first write in compression, therefore, emit the GZIP header
                _headerByteCount = EmitHeader();
            }
            else
            {
                throw new ArchiveOperationException();
            }
        }

        await BaseStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        if (BaseStream._streamMode == ZlibBaseStream.StreamMode.Undefined)
        {
            if (BaseStream._wantCompress)
            {
                // first write in compression, therefore, emit the GZIP header
                _headerByteCount = EmitHeader();
            }
            else
            {
                throw new ArchiveOperationException();
            }
        }

        await BaseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (BaseStream != null)
        {
            await BaseStream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif
}
