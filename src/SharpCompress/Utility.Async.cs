using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress;

internal static partial class Utility
{
    extension(Stream source)
    {
        /// <summary>
        /// Read exactly the requested number of bytes from a stream asynchronously. Throws EndOfStreamException if not enough data is available.
        /// </summary>
        public async ValueTask ReadExactAsync(
            byte[] buffer,
            int offset,
            int length,
            CancellationToken cancellationToken = default
        )
        {
#if LEGACY_DOTNET
            if (source is null)
            {
                throw new ArgumentNullException();
            }
#else
            ThrowHelper.ThrowIfNull(source);
#endif

            ThrowHelper.ThrowIfNull(buffer);

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            while (length > 0)
            {
                var fetched = await source
                    .ReadAsync(buffer, offset, length, cancellationToken)
                    .ConfigureAwait(false);
                if (fetched <= 0)
                {
                    throw new IncompleteArchiveException("Unexpected end of stream.");
                }

                offset += fetched;
                length -= fetched;
            }
        }

        public async ValueTask<long> TransferToAsync(
            Stream destination,
            long maxLength,
            CancellationToken cancellationToken = default
        )
        {
            // Use ReadOnlySubStream to limit reading and leverage framework's CopyToAsync
            using var limitedStream = new IO.ReadOnlySubStream(source, maxLength);
            await limitedStream
                .CopyToAsync(destination, Constants.BufferSize, cancellationToken)
                .ConfigureAwait(false);
            return limitedStream.Position;
        }

        public async ValueTask<bool> ReadFullyAsync(
            byte[] buffer,
            CancellationToken cancellationToken = default
        )
        {
            var total = 0;
            int read;
            while (
                (
                    read = await source
                        .ReadAsync(buffer, total, buffer.Length - total, cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }
            return (total >= buffer.Length);
        }

        public async ValueTask<bool> ReadFullyAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken = default
        )
        {
            var total = 0;
            int read;
            while (
                (
                    read = await source
                        .ReadAsync(buffer, offset + total, count - total, cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                total += read;
                if (total >= count)
                {
                    return true;
                }
            }
            return (total >= count);
        }
    }
}
