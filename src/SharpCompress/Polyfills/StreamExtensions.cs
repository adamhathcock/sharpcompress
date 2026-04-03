using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress;

public static partial class StreamExtensions
{
    extension(Stream stream)
    {
        public void Skip(long advanceAmount)
        {
            if (stream.CanSeek && stream is not SharpCompressStream)
            {
                stream.Position += advanceAmount;
                return;
            }

            using var readOnlySubStream = new ReadOnlySubStream(stream, advanceAmount);
            readOnlySubStream.CopyTo(Stream.Null);
        }

        public void Skip() => stream.CopyTo(Stream.Null);

        public Task SkipAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET5_0_OR_GREATER
            return stream.CopyToAsync(Stream.Null, cancellationToken);
#else
            return stream.CopyToAsync(Stream.Null);
#endif
        }

        internal int Read(Span<byte> buffer)
        {
            var temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                var read = stream.Read(temp, 0, buffer.Length);

                temp.AsSpan(0, read).CopyTo(buffer);

                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        internal void Write(ReadOnlySpan<byte> buffer)
        {
            var temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

            buffer.CopyTo(temp);

            try
            {
                stream.Write(temp, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public long TransferTo(Stream destination, long maxLength)
        {
            // Use ReadOnlySubStream to limit reading and leverage framework's CopyTo
            using var limitedStream = new IO.ReadOnlySubStream(stream, maxLength);
            limitedStream.CopyTo(destination, Constants.BufferSize);
            return limitedStream.Position;
        }

        public async ValueTask SkipAsync(
            long advanceAmount,
            CancellationToken cancellationToken = default
        )
        {
            if (stream.CanSeek && stream is not SharpCompressStream)
            {
                stream.Position += advanceAmount;
                return;
            }

            var array = ArrayPool<byte>.Shared.Rent(Constants.BufferSize);
            try
            {
                while (advanceAmount > 0)
                {
                    var toRead = (int)Math.Min(array.Length, advanceAmount);
                    var read = await stream
                        .ReadAsync(array, 0, toRead, cancellationToken)
                        .ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    advanceAmount -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

#if NET8_0_OR_GREATER
        public bool ReadFully(byte[] buffer)
        {
            try
            {
                stream.ReadExactly(buffer);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        public bool ReadFully(Span<byte> buffer)
        {
            try
            {
                stream.ReadExactly(buffer);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }
#else
        public bool ReadFully(byte[] buffer)
        {
            var total = 0;
            int read;
            while ((read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }

            return (total >= buffer.Length);
        }

        public bool ReadFully(Span<byte> buffer)
        {
            var total = 0;
            int read;
            while ((read = stream.Read(buffer.Slice(total, buffer.Length - total))) > 0)
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }

            return (total >= buffer.Length);
        }
#endif

        /// <summary>
        /// Read exactly the requested number of bytes from a stream. Throws EndOfStreamException if not enough data is available.
        /// </summary>
        public void ReadExact(byte[] buffer, int offset, int length)
        {
#if LEGACY_DOTNET
            if (stream is null)
            {
                throw new ArgumentNullException();
            }
#else
            ThrowHelper.ThrowIfNull(stream);
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
                var fetched = stream.Read(buffer, offset, length);
                if (fetched <= 0)
                {
                    throw new IncompleteArchiveException("Unexpected end of stream.");
                }

                offset += fetched;
                length -= fetched;
            }
        }
    }
}
