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

namespace SharpCompress.Compressors.Deflate64;

public sealed partial class Deflate64Stream : Stream
{
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
