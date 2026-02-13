using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.Utilities;

internal partial class CrcBuilderStream : Stream
{
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
        await _mTarget.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }
}
