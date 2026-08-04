// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Deflate64;

public sealed partial class Deflate64Stream : Stream
{
    private const int DEFAULT_BUFFER_SIZE = 8192;

    private Stream _stream;
    private InflaterManaged _inflater;
    private byte[] _buffer;

    public Deflate64Stream(Stream stream, CompressionMode mode)
    {
        ThrowHelper.ThrowIfNull(stream);

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

    private void ValidateParameters(byte[] array, int offset, int count)
    {
        ThrowHelper.ThrowIfNull(array);

        ThrowHelper.ThrowIfNegative(offset);

        ThrowHelper.ThrowIfNegative(count);

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
        throw new ArchiveOperationException("Deflate64: cannot write to this stream");

    public override void Write(byte[] buffer, int offset, int count) =>
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
