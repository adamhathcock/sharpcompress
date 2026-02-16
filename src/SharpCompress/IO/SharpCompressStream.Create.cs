using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO;

public partial class SharpCompressStream
{
    /// <summary>
    /// Creates a SharpCompressStream that acts as a passthrough wrapper.
    /// No buffering is performed; CanSeek delegates to the underlying stream.
    /// The underlying stream will not be disposed when this stream is disposed.
    /// </summary>
    public static SharpCompressStream CreateNonDisposing(Stream stream) =>
        new(stream, leaveStreamOpen: true, passthrough: true, bufferSize: null);

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
        return new SharpCompressStream(stream, false, false, bufferSize);
    }
}
