using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common;

public partial class EntryStream
{
    /// <summary>
    /// Asynchronously skip the rest of the entry stream.
    /// </summary>
    public async ValueTask SkipEntryAsync(CancellationToken cancellationToken = default)
    {
        await this.SkipAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        if (!(_completed || _reader.Cancelled))
        {
            await SkipEntryAsync().ConfigureAwait(false);
        }

        //Need a safe standard approach to this - it's okay for compression to overreads. Handling needs to be standardised
        if (_stream is IStreamStack ss)
        {
            if (ss.BaseStream() is SharpCompress.Compressors.Deflate.DeflateStream deflateStream)
            {
                await deflateStream.FlushAsync().ConfigureAwait(false);
            }
            else if (ss.BaseStream() is SharpCompress.Compressors.LZMA.LzmaStream lzmaStream)
            {
                await lzmaStream.FlushAsync().ConfigureAwait(false);
            }
        }
#if DEBUG_STREAMS
        this.DebugDispose(typeof(EntryStream));
#endif
        await base.DisposeAsync().ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
#endif

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var read = await _stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (read <= 0)
        {
            _completed = true;
        }
        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var read = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read <= 0)
        {
            _completed = true;
        }
        return read;
    }
#endif
}
