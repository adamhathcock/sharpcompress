using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress;

internal static partial class Utility
{
    /// <summary>
    /// Rents a buffer from the shared ArrayPool, reads data into it asynchronously, and executes a processor with the buffer.
    /// The buffer is automatically returned to the pool after use.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type (struct)</typeparam>
    /// <typeparam name="TResult">The return type</typeparam>
    /// <param name="stream">The stream to read from</param>
    /// <param name="size">The size of the buffer to rent and read</param>
    /// <param name="processor">The processor to execute with the rented buffer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the processor</returns>
    public static async ValueTask<TResult> ReadFullyRentedAsync<TProcessor, TResult>(
        this Stream stream,
        int size,
        TProcessor processor,
        CancellationToken cancellationToken = default
    )
        where TProcessor : struct, IBufferProcessor<TResult>
    {
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            if (
                !await stream
                    .ReadFullyAsync(buffer, 0, size, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                throw new EndOfStreamException();
            }
            return processor.Process(buffer.AsSpan(0, size));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Rents a buffer from the shared ArrayPool, reads data into it asynchronously, and executes a processor with the buffer.
    /// The buffer is automatically returned to the pool after use. Returns failure result if end of stream is reached.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type (struct)</typeparam>
    /// <typeparam name="TResult">The return type</typeparam>
    /// <param name="stream">The stream to read from</param>
    /// <param name="size">The size of the buffer to rent and read</param>
    /// <param name="processor">The processor to execute with the rented buffer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the processor (success or failure)</returns>
    public static async ValueTask<TResult> TryReadFullyRentedAsync<TProcessor, TResult>(
        this Stream stream,
        int size,
        TProcessor processor,
        CancellationToken cancellationToken = default
    )
        where TProcessor : struct, ITryBufferProcessor<TResult>
    {
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            if (
                !await stream
                    .ReadFullyAsync(buffer, 0, size, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                return processor.OnFailure();
            }
            return processor.OnSuccess(buffer.AsSpan(0, size));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Rents a buffer from the shared ArrayPool, reads exactly the specified amount of data asynchronously, and executes a processor with the buffer.
    /// The buffer is automatically returned to the pool after use. Throws EndOfStreamException if not enough data is available.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type (struct)</typeparam>
    /// <typeparam name="TResult">The return type</typeparam>
    /// <param name="stream">The stream to read from</param>
    /// <param name="size">The size of the buffer to rent and read</param>
    /// <param name="processor">The processor to execute with the rented buffer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the processor</returns>
    public static async ValueTask<TResult> ReadExactRentedAsync<TProcessor, TResult>(
        this Stream stream,
        int size,
        TProcessor processor,
        CancellationToken cancellationToken = default
    )
        where TProcessor : struct, IBufferProcessor<TResult>
    {
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            await stream.ReadExactAsync(buffer, 0, size, cancellationToken).ConfigureAwait(false);
            return processor.Process(buffer.AsSpan(0, size));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

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
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

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
                    throw new EndOfStreamException();
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
