using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Common.Zip.SOZip;

/// <summary>
/// A Deflate stream that inserts sync flush points at regular intervals
/// to enable random access (SOZip optimization).
/// </summary>
internal sealed class SOZipDeflateStream : Stream
{
    private readonly DeflateStream _deflateStream;
    private readonly Stream _baseStream;
    private readonly uint _chunkSize;
    private readonly List<ulong> _compressedOffsets = new();
    private readonly long _baseOffset;
    private long _uncompressedBytesWritten;
    private long _nextSyncPoint;
    private bool _disposed;

    /// <summary>
    /// Creates a new SOZip Deflate stream
    /// </summary>
    /// <param name="baseStream">The underlying stream to write to</param>
    /// <param name="compressionLevel">The compression level</param>
    /// <param name="chunkSize">The chunk size for sync flush points</param>
    public SOZipDeflateStream(Stream baseStream, CompressionLevel compressionLevel, int chunkSize)
    {
        _baseStream = baseStream;
        _chunkSize = (uint)chunkSize;
        _baseOffset = baseStream.Position;
        _nextSyncPoint = chunkSize;

        // Record the first offset (start of compressed data)
        _compressedOffsets.Add(0);

        _deflateStream = new DeflateStream(
            baseStream,
            CompressionMode.Compress,
            compressionLevel
        );
    }

    /// <summary>
    /// Gets the array of compressed offsets recorded during writing
    /// </summary>
    public ulong[] CompressedOffsets => _compressedOffsets.ToArray();

    /// <summary>
    /// Gets the total number of uncompressed bytes written
    /// </summary>
    public ulong UncompressedBytesWritten => (ulong)_uncompressedBytesWritten;

    /// <summary>
    /// Gets the total number of compressed bytes written
    /// </summary>
    public ulong CompressedBytesWritten => (ulong)(_baseStream.Position - _baseOffset);

    /// <summary>
    /// Gets the chunk size being used
    /// </summary>
    public uint ChunkSize => _chunkSize;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_disposed && _deflateStream.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _deflateStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SOZipDeflateStream));
        }

        var remaining = count;
        var currentOffset = offset;

        while (remaining > 0)
        {
            // Calculate how many bytes until the next sync point
            var bytesUntilSync = (int)(_nextSyncPoint - _uncompressedBytesWritten);

            if (bytesUntilSync <= 0)
            {
                // We've reached a sync point - perform sync flush
                PerformSyncFlush();
                continue;
            }

            // Write up to the next sync point
            var bytesToWrite = Math.Min(remaining, bytesUntilSync);
            _deflateStream.Write(buffer, currentOffset, bytesToWrite);

            _uncompressedBytesWritten += bytesToWrite;
            currentOffset += bytesToWrite;
            remaining -= bytesToWrite;
        }
    }

    private void PerformSyncFlush()
    {
        // Flush with Z_SYNC_FLUSH to create an independent block
        var originalFlushMode = _deflateStream.FlushMode;
        _deflateStream.FlushMode = FlushType.Sync;
        _deflateStream.Flush();
        _deflateStream.FlushMode = originalFlushMode;

        // Record the compressed offset for this sync point
        var compressedOffset = (ulong)(_baseStream.Position - _baseOffset);
        _compressedOffsets.Add(compressedOffset);

        // Set the next sync point
        _nextSyncPoint += _chunkSize;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _deflateStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
