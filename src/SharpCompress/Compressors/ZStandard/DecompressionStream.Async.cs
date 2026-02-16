using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public partial class DecompressionStream
{
#if !LEGACY_DOTNET
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
#else

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
#endif
    {
        EnsureNotDisposed();

        // Guard against infinite loop (output.pos would never become non-zero)
        if (buffer.Length == 0)
        {
            return 0;
        }

        var output = new ZSTD_outBuffer_s { pos = 0, size = (nuint)buffer.Length };
        while (true)
        {
            // If there is still input available, or there might be data buffered in the decompressor context, flush that out
            while (input.pos < input.size || !contextDrained)
            {
                nuint oldInputPos = input.pos;
                nuint result = DecompressStream(ref output, buffer.Span);
                if (output.pos > 0 || oldInputPos != input.pos)
                {
                    // Keep result from last decompress call that made some progress, so we known if we're at end of frame
                    lastDecompressResult = result;
                }
                // If decompression filled the output buffer, there might still be data buffered in the decompressor context
                contextDrained = output.pos < output.size;
                // If we have data to return, return it immediately, so we won't stall on Read
                if (output.pos > 0)
                {
                    return (int)output.pos;
                }
            }

            // Otherwise, read some more input
            int bytesRead;
            if (
                (
                    bytesRead = await innerStream
                        .ReadAsync(inputBuffer, 0, inputBufferSize, cancellationToken)
                        .ConfigureAwait(false)
                ) == 0
            )
            {
                if (checkEndOfStream && lastDecompressResult != 0)
                {
                    throw new IncompleteArchiveException("Premature end of stream");
                }

                return 0;
            }

            input.size = (nuint)bytesRead;
            input.pos = 0;
        }
    }
}
