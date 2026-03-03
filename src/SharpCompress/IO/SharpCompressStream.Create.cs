using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO;

public partial class SharpCompressStream
{
    /// <summary>
    /// Creates a <see cref="SharpCompressStream"/> that acts as a zero-overhead passthrough wrapper
    /// around <paramref name="stream"/> without taking ownership of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a thin wrapper: all reads, writes, and seeks are forwarded directly to the underlying
    /// stream with no ring-buffer overhead. <see cref="Stream.CanSeek"/> delegates to the underlying
    /// stream's own value.
    /// </para>
    /// <para>
    /// The resulting stream does <b>not</b> support <see cref="StartRecording"/>, <see cref="Rewind()"/>,
    /// or <see cref="StopRecording"/>. Call <see cref="Create"/> on the passthrough stream to obtain
    /// a recording-capable wrapper when needed.
    /// </para>
    /// <para>
    /// Because the stream does not take ownership, the underlying stream is <b>never</b> disposed when
    /// this wrapper is disposed. Use this when you need to satisfy an API that expects a
    /// <see cref="SharpCompressStream"/> without transferring lifetime responsibility.
    /// </para>
    /// </remarks>
    /// <param name="stream">The underlying stream to wrap. Must not be <see langword="null"/>.</param>
    /// <returns>
    /// A passthrough <see cref="SharpCompressStream"/> that does not dispose <paramref name="stream"/>.
    /// </returns>
    public static SharpCompressStream CreateNonDisposing(Stream stream) =>
        new(stream, leaveStreamOpen: true, passthrough: true, bufferSize: null);

    /// <summary>
    /// Creates a <see cref="SharpCompressStream"/> that supports recording and rewinding over
    /// <paramref name="stream"/>, choosing the most efficient strategy based on the stream's
    /// capabilities.
    /// </summary>
    /// <remarks>
    /// <para><b>Seekable streams</b> — wraps in a thin delegate that calls the underlying
    /// stream's native <see cref="Stream.Seek"/> directly. No ring buffer is allocated.
    /// <see cref="StartRecording"/> stores the current position; <see cref="Rewind()"/> seeks
    /// back to it.</para>
    /// <para><b>Non-seekable streams</b> (network streams, compressed streams, pipes) — allocates
    /// a ring buffer of <paramref name="bufferSize"/> bytes. All bytes read from the underlying
    /// stream are kept in the ring buffer so that <see cref="Rewind()"/> can replay them without
    /// re-reading the underlying stream. If more bytes have been read than the ring buffer can hold,
    /// a subsequent rewind will throw <see cref="InvalidOperationException"/>; increase
    /// <paramref name="bufferSize"/> or <see cref="Common.Constants.RewindableBufferSize"/> to
    /// avoid this.</para>
    /// <para><b>Already-wrapped streams</b> — if <paramref name="stream"/> is already a
    /// <see cref="SharpCompressStream"/> (or a stack that contains one), it is returned as-is to
    /// prevent double-wrapping and double-buffering.</para>
    /// </remarks>
    /// <param name="stream">The underlying stream to wrap. Must not be <see langword="null"/>.</param>
    /// <param name="bufferSize">
    /// Size in bytes of the ring buffer allocated for non-seekable streams.
    /// Defaults to <see cref="Common.Constants.RewindableBufferSize"/> (81 920 bytes) when
    /// <see langword="null"/>. Has no effect when <paramref name="stream"/> is seekable, because
    /// no ring buffer is needed in that case.
    /// </param>
    /// <returns>
    /// A <see cref="SharpCompressStream"/> wrapping <paramref name="stream"/>. The returned instance
    /// owns the stream and will dispose it unless the original source was a non-disposing passthrough
    /// wrapper.
    /// </returns>
    public static SharpCompressStream Create(Stream stream, int? bufferSize = null)
    {
        var rewindableBufferSize = bufferSize ?? Constants.RewindableBufferSize;

        // If it's a passthrough SharpCompressStream, unwrap it and create proper seekable wrapper
        if (stream is SharpCompressStream sharpCompressStream)
        {
            if (sharpCompressStream._isPassthrough)
            {
                // Unwrap the passthrough and create appropriate wrapper
                var underlying = sharpCompressStream.stream;
                if (underlying.CanSeek)
                {
                    // Create SeekableSharpCompressStream that preserves LeaveStreamOpen
                    return new SeekableSharpCompressStream(underlying, true);
                }
                // Non-seekable underlying stream - wrap with rolling buffer
                return new SharpCompressStream(underlying, true, false, rewindableBufferSize);
            }
            // Not passthrough - return as-is
            return sharpCompressStream;
        }

        // Check if stream is wrapping a SharpCompressStream (e.g., via IStreamStack)
        if (stream is IStreamStack streamStack)
        {
            var underlying = streamStack.GetStream<SharpCompressStream>();
            if (underlying is not null)
            {
                return underlying;
            }
        }

        if (stream.CanSeek)
        {
            return new SeekableSharpCompressStream(stream);
        }

        // For non-seekable streams, create a SharpCompressStream with rolling buffer
        // to allow limited backward seeking (required by decompressors that over-read)
        return new SharpCompressStream(stream, false, false, rewindableBufferSize);
    }
}
