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

    /// <summary>
    /// Opens a file stream for asynchronous writing.
    /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
    /// Falls back to FileStream constructor with async options on legacy frameworks.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FileStream configured for asynchronous operations.</returns>
    public static Stream OpenAsyncWriteStream(
        string path,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

#if !LEGACY_DOTNET
        // Use File.OpenHandle with async options for .NET 8.0+
        var handle = File.OpenHandle(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileOptions.Asynchronous
        );
        return new FileStream(handle, FileAccess.Write);
#else
        // For legacy .NET, use FileStream constructor with async options
        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096, //default
            FileOptions.Asynchronous
        );
#endif
    }

    /// <summary>
    /// Opens a file stream for asynchronous writing from a FileInfo.
    /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
    /// Falls back to FileStream constructor with async options on legacy frameworks.
    /// </summary>
    /// <param name="fileInfo">The FileInfo to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FileStream configured for asynchronous operations.</returns>
    public static Stream OpenAsyncWriteStream(
        this FileInfo fileInfo,
        CancellationToken cancellationToken
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenAsyncWriteStream(fileInfo.FullName, cancellationToken);
    }

    /// <summary>
    /// Opens a file stream for asynchronous reading.
    /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
    /// Falls back to FileStream constructor with async options on legacy frameworks.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FileStream configured for asynchronous operations.</returns>
    public static Stream OpenAsyncReadStream(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

#if !LEGACY_DOTNET
        // Use File.OpenHandle with async options for .NET 8.0+
        var handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.Asynchronous
        );
        return new FileStream(handle, FileAccess.Read);
#else
        // For legacy .NET, use FileStream constructor with async options
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous
        );
#endif
    }

    /// <summary>
    /// Opens a file stream for asynchronous reading from a FileInfo.
    /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
    /// Falls back to FileStream constructor with async options on legacy frameworks.
    /// </summary>
    /// <param name="fileInfo">The FileInfo to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FileStream configured for asynchronous operations.</returns>
    public static Stream OpenAsyncReadStream(
        this FileInfo fileInfo,
        CancellationToken cancellationToken
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenAsyncReadStream(fileInfo.FullName, cancellationToken);
    }
}
