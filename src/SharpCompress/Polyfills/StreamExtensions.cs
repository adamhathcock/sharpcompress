using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress;

public static class StreamExtensions
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

        /// <summary>
        /// Returns a scope that disposes this stream when awaited, asynchronously where the runtime type
        /// supports it. Lets <c>await using</c> be written against a <see cref="Stream"/>-typed local on
        /// every target framework.
        /// </summary>
        internal AsyncDisposeScope DisposeAsyncScope() => new(stream);

        /// <summary>
        /// Disposes this stream, asynchronously where the runtime type supports it. Use where the static
        /// type is <see cref="Stream"/>, which has no <c>DisposeAsync</c> on .NET Framework 4.8 /
        /// .NET Standard 2.0.
        /// </summary>
        internal ValueTask DisposeAsyncCompat() => new AsyncDisposeScope(stream).DisposeAsync();

        public async ValueTask SkipAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET6_0_OR_GREATER
            await stream.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
#else
            await stream.CopyToAsync(Stream.Null).ConfigureAwait(false);
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
    }
}
