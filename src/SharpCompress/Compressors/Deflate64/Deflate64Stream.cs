// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate64;

public sealed class Deflate64Stream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private const int DEFAULT_BUFFER_SIZE = 8192;

    private Stream _stream;
    private InflaterManaged _inflater;
    private byte[] _buffer;

    public Deflate64Stream(Stream stream, CompressionMode mode)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (mode != CompressionMode.Decompress)
        {
            throw new NotImplementedException(
                "Deflate64: this implementation only supports decompression"
            );
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Deflate64: input stream is not readable", nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Deflate64: input stream is not readable", nameof(stream));
        }

        _inflater = new InflaterManaged(true);

        _stream = stream;
        _buffer = new byte[DEFAULT_BUFFER_SIZE];
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(Deflate64Stream));
#endif
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanWrite => false;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException("Deflate64: not supported");

    public override long Position
    {
        get => throw new NotSupportedException("Deflate64: not supported");
        set => throw new NotSupportedException("Deflate64: not supported");
    }

    public override void Flush() => EnsureNotDisposed();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("Deflate64: not supported");

    public override void SetLength(long value) =>
        throw new NotSupportedException("Deflate64: not supported");

    public override int Read(byte[] array, int offset, int count)
    {
        ValidateParameters(array, offset, count);
        EnsureNotDisposed();

        int bytesRead;
        var currentOffset = offset;
        var remainingCount = count;

        while (true)
        {
            bytesRead = _inflater.Inflate(array, currentOffset, remainingCount);
            currentOffset += bytesRead;
            remainingCount -= bytesRead;

            if (remainingCount == 0)
            {
                break;
            }

            if (_inflater.Finished())
            {
                // if we finished decompressing, we can't have anything left in the outputwindow.
                Debug.Assert(
                    _inflater.AvailableOutput == 0,
                    "We should have copied all stuff out!"
                );
                break;
            }

            var bytes = _stream.Read(_buffer, 0, _buffer.Length);
            if (bytes <= 0)
            {
                break;
            }
            else if (bytes > _buffer.Length)
            {
                // The stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the buffer supplied to it.
                throw new InvalidFormatException("Deflate64: invalid data");
            }

            _inflater.SetInput(_buffer, 0, bytes);
        }

        return count - remainingCount;
    }

    public override async Task<int> ReadAsync(
        byte[] array,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ValidateParameters(array, offset, count);
        EnsureNotDisposed();

        int bytesRead;
        var currentOffset = offset;
        var remainingCount = count;

        while (true)
        {
            bytesRead = _inflater.Inflate(array, currentOffset, remainingCount);
            currentOffset += bytesRead;
            remainingCount -= bytesRead;

            if (remainingCount == 0)
            {
                break;
            }

            if (_inflater.Finished())
            {
                // if we finished decompressing, we can't have anything left in the outputwindow.
                Debug.Assert(
                    _inflater.AvailableOutput == 0,
                    "We should have copied all stuff out!"
                );
                break;
            }

            var bytes = await _stream
                .ReadAsync(_buffer, 0, _buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            if (bytes <= 0)
            {
                break;
            }
            else if (bytes > _buffer.Length)
            {
                // The stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the buffer supplied to it.
                throw new InvalidFormatException("Deflate64: invalid data");
            }

            _inflater.SetInput(_buffer, 0, bytes);
        }

        return count - remainingCount;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        EnsureNotDisposed();

        // InflaterManaged doesn't have a Span-based Inflate method, so we need to work with arrays
        // For large buffers, we could rent from ArrayPool, but for simplicity we'll use the buffer's array if available
        if (
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>(
                buffer,
                out var arraySegment
            )
        )
        {
            // Fast path: the Memory<byte> is backed by an array
            return await ReadAsync(
                    arraySegment.Array!,
                    arraySegment.Offset,
                    arraySegment.Count,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            // Slow path: rent a temporary array
            var tempBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var bytesRead = await ReadAsync(tempBuffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false);
                tempBuffer.AsMemory(0, bytesRead).CopyTo(buffer);
                return bytesRead;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }
    }
#endif

    private void ValidateParameters(byte[] array, int offset, int count)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (array.Length - offset < count)
        {
            throw new ArgumentException("Deflate64: invalid offset/count combination");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_stream is null)
        {
            ThrowStreamClosedException();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowStreamClosedException() =>
        throw new ObjectDisposedException(null, "Deflate64: stream has been disposed");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCannotWriteToDeflateManagedStreamException() =>
        throw new InvalidOperationException("Deflate64: cannot write to this stream");

    public override void Write(byte[] array, int offset, int count) =>
        ThrowCannotWriteToDeflateManagedStreamException();

    // This is called by Dispose:
    private void PurgeBuffers(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (_stream is null)
        {
            return;
        }

        Flush();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            PurgeBuffers(disposing);
        }
        finally
        {
            // Close the underlying stream even if PurgeBuffers threw.
            // Stream.Close() may throw here (may or may not be due to the same error).
            // In this case, we still need to clean up internal resources, hence the inner finally blocks.
            try
            {
#if DEBUG_STREAMS
                this.DebugDispose(typeof(Deflate64Stream));
#endif
                if (disposing)
                {
                    _stream.Dispose();
                }
            }
            finally
            {
                try
                {
                    _inflater.Dispose();
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }
    }
}
