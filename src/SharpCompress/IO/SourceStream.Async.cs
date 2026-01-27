using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

public partial class SourceStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count <= 0)
        {
            return 0;
        }

        var total = count;
        var r = -1;

        while (count != 0 && r != 0)
        {
            r = await Current
                .ReadAsync(
                    buffer,
                    offset,
                    (int)Math.Min(count, Current.Length - Current.Position),
                    cancellationToken
                )
                .ConfigureAwait(false);
            count -= r;
            offset += r;

            if (!IsVolumes && count != 0 && Current.Position == Current.Length)
            {
                var length = Current.Length;

                // Load next file if present
                if (!SetStream(_stream + 1))
                {
                    break;
                }

                // Current stream switched
                // Add length of previous stream
                _prevSize += length;
                Current.Seek(0, SeekOrigin.Begin);
                r = -1; //BugFix: reset to allow loop if count is still not 0 - was breaking split zipx (lzma xz etc)
            }
        }

        return total - count;
    }

#if !LEGACY_DOTNET

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.Length <= 0)
        {
            return 0;
        }

        var total = buffer.Length;
        var count = buffer.Length;
        var offset = 0;
        var r = -1;

        while (count != 0 && r != 0)
        {
            r = await Current
                .ReadAsync(
                    buffer.Slice(offset, (int)Math.Min(count, Current.Length - Current.Position)),
                    cancellationToken
                )
                .ConfigureAwait(false);
            count -= r;
            offset += r;

            if (!IsVolumes && count != 0 && Current.Position == Current.Length)
            {
                var length = Current.Length;

                // Load next file if present
                if (!SetStream(_stream + 1))
                {
                    break;
                }

                // Current stream switched
                // Add length of previous stream
                _prevSize += length;
                Current.Seek(0, SeekOrigin.Begin);
                r = -1;
            }
        }

        return total - count;
    }
#endif
}
