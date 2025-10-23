using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Performance;

/// <summary>
/// A Stream implementation backed by a List of byte arrays that supports large position values.
/// This allows handling streams larger than typical 32-bit or even standard 64-bit constraints
/// by chunking data into multiple byte array segments.
/// </summary>
public class LargeMemoryStream : Stream
{
    private readonly List<byte[]> _chunks;
    private readonly int _chunkSize;
    private long _position;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the LargeMemoryStream class.
    /// </summary>
    /// <param name="chunkSize">The size of each chunk in the backing byte array list. Defaults to 1MB.</param>
    public LargeMemoryStream(int chunkSize = 1024 * 1024)
    {
        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));

        _chunks = new List<byte[]>();
        _chunkSize = chunkSize;
        _position = 0;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            if (_chunks.Count == 0)
                return 0;

            long length = (long)(_chunks.Count - 1) * _chunkSize;
            length += _chunks[_chunks.Count - 1].Length;
            return length;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _position;
        }
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Position cannot be negative."
                );
            _position = value;
        }
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        // No-op for in-memory stream
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        long length = Length;
        if (_position >= length)
            return 0;

        int bytesToRead = (int)Math.Min(count, length - _position);
        int bytesRead = 0;

        while (bytesRead < bytesToRead)
        {
            long chunkIndex = _position / _chunkSize;
            int chunkOffset = (int)(_position % _chunkSize);

            if (chunkIndex >= _chunks.Count)
                break;

            byte[] chunk = _chunks[(int)chunkIndex];
            int availableInChunk = chunk.Length - chunkOffset;
            int bytesToCopyFromChunk = Math.Min(availableInChunk, bytesToRead - bytesRead);

            Array.Copy(chunk, chunkOffset, buffer, offset + bytesRead, bytesToCopyFromChunk);

            _position += bytesToCopyFromChunk;
            bytesRead += bytesToCopyFromChunk;
        }

        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        int bytesWritten = 0;

        while (bytesWritten < count)
        {
            long chunkIndex = _position / _chunkSize;
            int chunkOffset = (int)(_position % _chunkSize);

            // Ensure we have enough chunks
            while (_chunks.Count <= chunkIndex)
            {
                _chunks.Add(new byte[_chunkSize]);
            }

            byte[] chunk = _chunks[(int)chunkIndex];
            int availableInChunk = chunk.Length - chunkOffset;
            int bytesToCopyToChunk = Math.Min(availableInChunk, count - bytesWritten);

            Array.Copy(buffer, offset + bytesWritten, chunk, chunkOffset, bytesToCopyToChunk);

            _position += bytesToCopyToChunk;
            bytesWritten += bytesToCopyToChunk;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (newPosition < 0)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Cannot seek before the beginning of the stream."
            );

        _position = newPosition;
        return _position;
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Length cannot be negative.");

        long currentLength = Length;

        if (value < currentLength)
        {
            // Truncate
            long chunkIndex = (value + _chunkSize - 1) / _chunkSize;
            if (chunkIndex > 0)
                chunkIndex--;

            _chunks.RemoveRange((int)(chunkIndex + 1), _chunks.Count - (int)(chunkIndex + 1));

            if (chunkIndex < _chunks.Count)
            {
                int lastChunkSize = (int)(value - chunkIndex * _chunkSize);
                var x = _chunks[(int)chunkIndex];
                Array.Resize(ref x, lastChunkSize);
            }

            if (_position > value)
                _position = value;
        }
        else if (value > currentLength)
        {
            // Extend with zeros
            long chunkIndex = currentLength / _chunkSize;
            int chunkOffset = (int)(currentLength % _chunkSize);

            while ((long)_chunks.Count * _chunkSize < value)
            {
                _chunks.Add(new byte[_chunkSize]);
            }

            // Resize the last chunk if needed
            if (_chunks.Count > 0)
            {
                long lastChunkNeededSize = value - (long)(_chunks.Count - 1) * _chunkSize;
                if (lastChunkNeededSize < _chunkSize)
                {
                    var x = _chunks[^1];
                    Array.Resize(ref x, (int)lastChunkNeededSize);
                }
            }
        }
    }

    /// <summary>
    /// Gets the number of chunks in the backing list.
    /// </summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>
    /// Gets the size of each chunk in bytes.
    /// </summary>
    public int ChunkSize => _chunkSize;

    /// <summary>
    /// Converts the stream contents to a single byte array.
    /// This may consume significant memory for large streams.
    /// </summary>
    public byte[] ToArray()
    {
        ThrowIfDisposed();
        long length = Length;
        byte[] result = new byte[length];
        long currentPosition = _position;

        try
        {
            _position = 0;
            int totalRead = 0;
            while (totalRead < length)
            {
                int bytesToRead = (int)Math.Min(length - totalRead, int.MaxValue);
                int bytesRead = Read(result, totalRead, bytesToRead);
                if (bytesRead == 0)
                    break;
                totalRead += bytesRead;
            }
        }
        finally
        {
            _position = currentPosition;
        }

        return result;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _chunks.Clear();
            }
            _isDisposed = true;
        }

        base.Dispose(disposing);
    }
}
