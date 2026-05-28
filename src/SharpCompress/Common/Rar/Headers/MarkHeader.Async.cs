using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Rar.Headers;

internal partial class MarkHeader
{
    private static async ValueTask<byte> GetByteAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[1];
        var bytesRead = await stream
            .ReadAsync(buffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead == 1)
        {
            return buffer[0];
        }
        throw new IncompleteArchiveException("Unexpected end of stream.");
    }

    public static async ValueTask<MarkHeader> ReadAsync(
        Stream stream,
        bool leaveStreamOpen,
        bool lookForHeader,
        CancellationToken cancellationToken = default
    )
    {
        var maxScanIndex = lookForHeader ? MAX_SFX_SIZE : 0;
        try
        {
            var start = -1;
            var b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
            start++;
            while (start <= maxScanIndex)
            {
                if (b == 0x52)
                {
                    b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                    start++;
                    if (b == 0x61)
                    {
                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b != 0x72)
                        {
                            continue;
                        }

                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b != 0x21)
                        {
                            continue;
                        }

                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b != 0x1a)
                        {
                            continue;
                        }

                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b != 0x07)
                        {
                            continue;
                        }

                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b == 1)
                        {
                            b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                            start++;
                            if (b != 0)
                            {
                                continue;
                            }

                            return new MarkHeader(true); // Rar5
                        }
                        else if (b == 0)
                        {
                            return new MarkHeader(false); // Rar4
                        }
                    }
                    else if (b == 0x45)
                    {
                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b != 0x7e)
                        {
                            continue;
                        }

                        b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                        start++;
                        if (b != 0x5e)
                        {
                            continue;
                        }

                        throw new InvalidFormatException(
                            "Rar format version pre-4 is unsupported."
                        );
                    }
                }
                else
                {
                    b = await GetByteAsync(stream, cancellationToken).ConfigureAwait(false);
                    start++;
                }
            }
        }
        catch (Exception e)
        {
            if (!leaveStreamOpen)
            {
#if LEGACY_DOTNET && !NETSTANDARD2_1
                stream.Dispose();
#else
                await stream.DisposeAsync().ConfigureAwait(false);
#endif
            }
            throw new InvalidFormatException("Error trying to read rar signature.", e);
        }

        throw new InvalidFormatException("Rar signature not found");
    }
}
