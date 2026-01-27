using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.ZStandard;

internal partial class ZStandardStream
{
    internal static async ValueTask<bool> IsZStandardAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new byte[4];
        var bytesRead = await stream.ReadAsync(buffer, 0, 4, cancellationToken);
        if (bytesRead < 4)
        {
            return false;
        }

        var magic = BitConverter.ToUInt32(buffer, 0);
        if (ZstandardConstants.MAGIC != magic)
        {
            return false;
        }
        return true;
    }
}
