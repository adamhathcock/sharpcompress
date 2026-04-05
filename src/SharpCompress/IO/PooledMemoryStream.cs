using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

/// <summary>
/// MemoryStream implementation backed by pooled byte arrays.
/// </summary>
public sealed class PooledMemoryStream : MemoryStream
{
    private const int MaxStreamLength = int.MaxValue;

    private enum StorageMode
    {
        Segmented,
        Contiguous,
    }

    private readonly ArrayPool<byte> _arrayPool;
    private readonly int _blockSize;

    private readonly List<byte[]> _detachedExposedBuffers = new();

    private StorageMode _mode;
    private List<byte[]>? _blocks;
    private byte[]? _contiguousBuffer;

    private bool _contiguousBufferExposed;
    private bool _isOpen;
    private int _position;
    private int _length;
    private int _capacity;
    private int _allocatedCapacity;

    public PooledMemoryStream()
        : this(0) { }

    public PooledMemoryStream(int capacity)
        : this(capacity, Constants.BufferSize, ArrayPool<byte>.Shared) { }

    public PooledMemoryStream(int capacity, int blockSize)
        : this(capacity, blockSize, ArrayPool<byte>.Shared) { }

    public PooledMemoryStream(int capacity, int blockSize, ArrayPool<byte> arrayPool)
    {
        ThrowHelper.ThrowIfNull(arrayPool, nameof(arrayPool));
        ThrowHelper.ThrowIfNegative(capacity, nameof(capacity));
        ThrowHelper.ThrowIfNegativeOrZero(blockSize, nameof(blockSize));

        _arrayPool = arrayPool;
        _blockSize = blockSize;

        _mode = StorageMode.Segmented;
        _blocks = new List<byte[]>();
        _isOpen = true;
        _position = 0;
        _length = 0;
        _capacity = capacity;

        EnsureSegmentedAllocated(capacity);
    }

    public override bool CanRead => _isOpen;

    public override bool CanSeek => _isOpen;

    public override bool CanWrite => _isOpen;

    public override long Length
    {
        get
        {
            EnsureNotClosed();
            return _length;
        }
    }

    public override long Position
    {
        get
        {
            EnsureNotClosed();
            return _position;
        }
        set
        {
            ThrowHelper.ThrowIfNegative(value, nameof(value));
            EnsureNotClosed();
            ThrowHelper.ThrowIfGreaterThan(value, MaxStreamLength, nameof(value));

            _position = (int)value;
        }
    }

    public override int Capacity
    {
        get
        {
            EnsureNotClosed();
            return _capacity;
        }
        set
        {
            ThrowHelper.ThrowIfLessThan(value, _length, nameof(value));

            EnsureNotClosed();

            var target = value;
            if (target == _capacity)
            {
                return;
            }

            SetCapacityAbsolute(target);
        }
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin loc)
    {
        EnsureNotClosed();

        var anchor = loc switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => _position,
            SeekOrigin.End => _length,
            _ => throw new ArgumentException("Invalid seek origin.", nameof(loc)),
        };

        var target = anchor + offset;
        if (target < 0)
        {
            throw new IOException("Attempted to seek before the beginning of the stream.");
        }

        if (target > MaxStreamLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        _position = (int)target;
        return _position;
    }

    public override void SetLength(long value)
    {
        ThrowHelper.ThrowIfNegative(value, nameof(value));
        ThrowHelper.ThrowIfGreaterThan(value, MaxStreamLength, nameof(value));

        EnsureWritable();

        var newLength = (int)value;
        if (newLength > _capacity)
        {
            EnsureCapacityForAppend(newLength);
        }

        if (newLength > _length)
        {
            ClearRange(_length, newLength - _length);
        }

        _length = newLength;
        if (_position > newLength)
        {
            _position = newLength;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateReadWriteBufferArguments(buffer, offset, count);
        EnsureNotClosed();

        var available = _length - _position;
        if (available <= 0)
        {
            return 0;
        }

        if (count > available)
        {
            count = available;
        }

        switch (_mode)
        {
            case StorageMode.Contiguous:
                Buffer.BlockCopy(_contiguousBuffer!, _position, buffer, offset, count);
                break;
            case StorageMode.Segmented:
                CopyFromSegmented(_position, buffer, offset, count);
                break;
        }

        _position += count;
        return count;
    }

    public override int ReadByte()
    {
        EnsureNotClosed();
        if (_position >= _length)
        {
            return -1;
        }

        byte value;
        switch (_mode)
        {
            case StorageMode.Contiguous:
                value = _contiguousBuffer![_position];
                break;
            default:
            {
                var blockIndex = _position / _blockSize;
                var blockOffset = _position % _blockSize;
                value = _blocks![blockIndex][blockOffset];
                break;
            }
        }

        _position++;
        return value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateReadWriteBufferArguments(buffer, offset, count);
        EnsureWritable();

        if (count == 0)
        {
            return;
        }

        var endPosition = _position + count;
        if (endPosition < 0)
        {
            throw new IOException("Stream is too long.");
        }

        if (endPosition > _capacity)
        {
            EnsureCapacityForAppend(endPosition);
        }

        if (_position > _length)
        {
            ClearRange(_length, _position - _length);
        }

        switch (_mode)
        {
            case StorageMode.Contiguous:
                Buffer.BlockCopy(buffer, offset, _contiguousBuffer!, _position, count);
                break;
            case StorageMode.Segmented:
                CopyToSegmented(_position, buffer, offset, count);
                break;
        }

        _position = endPosition;
        if (_position > _length)
        {
            _length = _position;
        }
    }

    public override void WriteByte(byte value)
    {
        EnsureWritable();

        var endPosition = _position + 1;
        if (endPosition < 0)
        {
            throw new IOException("Stream is too long.");
        }

        if (endPosition > _capacity)
        {
            EnsureCapacityForAppend(endPosition);
        }

        if (_position > _length)
        {
            ClearRange(_length, _position - _length);
        }

        switch (_mode)
        {
            case StorageMode.Contiguous:
                _contiguousBuffer![_position] = value;
                break;
            default:
            {
                var blockIndex = _position / _blockSize;
                var blockOffset = _position % _blockSize;
                _blocks![blockIndex][blockOffset] = value;
                break;
            }
        }

        _position = endPosition;
        if (_position > _length)
        {
            _length = _position;
        }
    }

    private byte[] GetExposableContiguousBuffer()
    {
        EnsureContiguous();

        if (_capacity > _length)
        {
            ClearRange(_length, _capacity - _length);
        }

        _contiguousBufferExposed = true;
        return _contiguousBuffer!;
    }

    public override byte[] GetBuffer()
    {
        EnsureNotClosed();
        return GetExposableContiguousBuffer();
    }

    public override bool TryGetBuffer(out ArraySegment<byte> buffer)
    {
        EnsureNotClosed();

        var exposableBuffer = GetExposableContiguousBuffer();
        buffer = new ArraySegment<byte>(exposableBuffer, 0, _length);
        return true;
    }

    public override byte[] ToArray()
    {
        EnsureNotClosed();

        var count = _length;
        if (count == 0)
        {
            return Array.Empty<byte>();
        }

        var copy = new byte[count];
        switch (_mode)
        {
            case StorageMode.Contiguous:
                Buffer.BlockCopy(_contiguousBuffer!, 0, copy, 0, count);
                break;
            case StorageMode.Segmented:
                CopyFromSegmented(0, copy, 0, count);
                break;
        }

        return copy;
    }

    public override void WriteTo(Stream stream)
    {
        ThrowHelper.ThrowIfNull(stream, nameof(stream));
        EnsureNotClosed();

        var count = _length;
        if (count == 0)
        {
            return;
        }

        switch (_mode)
        {
            case StorageMode.Contiguous:
                stream.Write(_contiguousBuffer!, 0, count);
                break;
            case StorageMode.Segmented:
            {
                var position = 0;
                var remaining = count;
                while (remaining > 0)
                {
                    var blockIndex = position / _blockSize;
                    var blockOffset = position % _blockSize;
                    var toWrite = Math.Min(remaining, _blockSize - blockOffset);
                    stream.Write(_blocks![blockIndex], blockOffset, toWrite);
                    position += toWrite;
                    remaining -= toWrite;
                }

                break;
            }
        }
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }

        try
        {
            return Task.FromResult(Read(buffer, offset, count));
        }
        catch (Exception ex)
        {
            return Task.FromException<int>(ex);
        }
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        try
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

#if !LEGACY_DOTNET
    public override int Read(Span<byte> destination)
    {
        EnsureNotClosed();

        var available = _length - _position;
        if (available <= 0)
        {
            return 0;
        }

        var count = Math.Min(available, destination.Length);
        switch (_mode)
        {
            case StorageMode.Contiguous:
                _contiguousBuffer.AsSpan(_position, count).CopyTo(destination);
                break;
            case StorageMode.Segmented:
            {
                var sourcePosition = _position;
                var destinationOffset = 0;
                var remaining = count;

                while (remaining > 0)
                {
                    var blockIndex = sourcePosition / _blockSize;
                    var blockOffset = sourcePosition % _blockSize;
                    var toCopy = Math.Min(remaining, _blockSize - blockOffset);
                    _blocks!
                        [blockIndex]
                        .AsSpan(blockOffset, toCopy)
                        .CopyTo(destination.Slice(destinationOffset, toCopy));

                    sourcePosition += toCopy;
                    destinationOffset += toCopy;
                    remaining -= toCopy;
                }

                break;
            }
        }

        _position += count;
        return count;
    }

    public override void Write(ReadOnlySpan<byte> source)
    {
        EnsureWritable();
        if (source.Length == 0)
        {
            return;
        }

        var endPosition = _position + source.Length;
        if (endPosition < 0)
        {
            throw new IOException("Stream is too long.");
        }

        if (endPosition > _capacity)
        {
            EnsureCapacityForAppend(endPosition);
        }

        if (_position > _length)
        {
            ClearRange(_length, _position - _length);
        }

        switch (_mode)
        {
            case StorageMode.Contiguous:
                source.CopyTo(_contiguousBuffer.AsSpan(_position, source.Length));
                break;
            case StorageMode.Segmented:
            {
                var sourceOffset = 0;
                var destinationPosition = _position;
                var remaining = source.Length;

                while (remaining > 0)
                {
                    var blockIndex = destinationPosition / _blockSize;
                    var blockOffset = destinationPosition % _blockSize;
                    var toCopy = Math.Min(remaining, _blockSize - blockOffset);

                    source
                        .Slice(sourceOffset, toCopy)
                        .CopyTo(_blocks![blockIndex].AsSpan(blockOffset, toCopy));

                    sourceOffset += toCopy;
                    destinationPosition += toCopy;
                    remaining -= toCopy;
                }

                break;
            }
        }

        _position = endPosition;
        if (_position > _length)
        {
            _length = _position;
        }
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        try
        {
            return ValueTask.FromResult(Read(destination.Span));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<int>(ex);
        }
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        try
        {
            Write(source.Span);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (_isOpen)
        {
            _isOpen = false;

            if (disposing)
            {
                ReturnPooledBuffers();
            }
        }

        base.Dispose(disposing);
    }

    private void EnsureNotClosed()
    {
        if (!_isOpen)
        {
            throw new ObjectDisposedException(nameof(PooledMemoryStream));
        }
    }

    private void EnsureWritable()
    {
        EnsureNotClosed();
    }

    private void EnsureCapacityForAppend(int requiredLength)
    {
        if (requiredLength < 0)
        {
            throw new IOException("Stream is too long.");
        }

        if (requiredLength <= _capacity)
        {
            return;
        }

        var nextCapacity = RoundUpToBlockBoundary(requiredLength);
        SetCapacityAbsolute(nextCapacity);
    }

    private void SetCapacityAbsolute(int newCapacity)
    {
        ThrowHelper.ThrowIfLessThan(newCapacity, _length, nameof(newCapacity));

        switch (_mode)
        {
            case StorageMode.Contiguous:
                if (newCapacity > _allocatedCapacity)
                {
                    DemoteContiguousToSegmented();
                    EnsureSegmentedAllocated(newCapacity);
                }
                break;

            case StorageMode.Segmented:
                EnsureSegmentedAllocated(newCapacity);
                break;
        }

        _capacity = newCapacity;
        if (_length > _capacity)
        {
            _length = _capacity;
        }
        if (_position > _capacity)
        {
            _position = _capacity;
        }
    }

    private void EnsureContiguous()
    {
        if (_mode == StorageMode.Contiguous)
        {
            return;
        }

        var requested = Math.Max(_capacity, 1);
        var contiguous = _arrayPool.Rent(requested);
        if (_length > 0)
        {
            CopyFromSegmented(0, contiguous, 0, _length);
        }

        ReturnSegmentedBlocks();

        _mode = StorageMode.Contiguous;
        _contiguousBuffer = contiguous;
        _contiguousBufferExposed = false;
        _allocatedCapacity = contiguous.Length;
    }

    private void DemoteContiguousToSegmented()
    {
        var contiguous = _contiguousBuffer;
        if (contiguous is null)
        {
            return;
        }

        var requiredCapacity = Math.Max(_capacity, _length);
        _mode = StorageMode.Segmented;
        _blocks = new List<byte[]>();
        _contiguousBuffer = null;
        EnsureSegmentedAllocated(requiredCapacity);

        if (_length > 0)
        {
            CopyToSegmented(0, contiguous, 0, _length);
        }

        if (_contiguousBufferExposed)
        {
            _detachedExposedBuffers.Add(contiguous);
            _contiguousBufferExposed = false;
        }
        else
        {
            _arrayPool.Return(contiguous);
        }
    }

    private void EnsureSegmentedAllocated(int capacity)
    {
        if (_mode != StorageMode.Segmented)
        {
            throw new InvalidOperationException(
                "Segmented allocation requested while not in segmented mode."
            );
        }

        var requiredAllocated = RoundUpToBlockBoundary(capacity);
        var requiredBlocks = requiredAllocated == 0 ? 0 : requiredAllocated / _blockSize;

        _blocks ??= new List<byte[]>();

        while (_blocks.Count < requiredBlocks)
        {
            _blocks.Add(_arrayPool.Rent(_blockSize));
        }

        while (_blocks.Count > requiredBlocks)
        {
            var index = _blocks.Count - 1;
            var block = _blocks[index];
            _blocks.RemoveAt(index);
            _arrayPool.Return(block);
        }

        _allocatedCapacity = requiredAllocated;
    }

    private int RoundUpToBlockBoundary(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var rounded = ((long)value + _blockSize - 1) / _blockSize * _blockSize;
        if (rounded > MaxStreamLength)
        {
            throw new IOException("Stream is too long.");
        }

        return (int)rounded;
    }

    private void ClearRange(int absoluteStart, int count)
    {
        if (count <= 0)
        {
            return;
        }

        switch (_mode)
        {
            case StorageMode.Contiguous:
                Array.Clear(_contiguousBuffer!, absoluteStart, count);
                break;
            case StorageMode.Segmented:
            {
                var position = absoluteStart;
                var remaining = count;
                while (remaining > 0)
                {
                    var blockIndex = position / _blockSize;
                    var blockOffset = position % _blockSize;
                    var toClear = Math.Min(remaining, _blockSize - blockOffset);
                    Array.Clear(_blocks![blockIndex], blockOffset, toClear);
                    position += toClear;
                    remaining -= toClear;
                }

                break;
            }
        }
    }

    private void CopyFromSegmented(
        int absoluteSourcePosition,
        byte[] destination,
        int offset,
        int count
    )
    {
        var sourcePosition = absoluteSourcePosition;
        var destinationOffset = offset;
        var remaining = count;

        while (remaining > 0)
        {
            var blockIndex = sourcePosition / _blockSize;
            var blockOffset = sourcePosition % _blockSize;
            var toCopy = Math.Min(remaining, _blockSize - blockOffset);
            Buffer.BlockCopy(
                _blocks![blockIndex],
                blockOffset,
                destination,
                destinationOffset,
                toCopy
            );

            sourcePosition += toCopy;
            destinationOffset += toCopy;
            remaining -= toCopy;
        }
    }

    private void CopyToSegmented(
        int absoluteDestinationPosition,
        byte[] source,
        int offset,
        int count
    )
    {
        var sourceOffset = offset;
        var destinationPosition = absoluteDestinationPosition;
        var remaining = count;

        while (remaining > 0)
        {
            var blockIndex = destinationPosition / _blockSize;
            var blockOffset = destinationPosition % _blockSize;
            var toCopy = Math.Min(remaining, _blockSize - blockOffset);
            Buffer.BlockCopy(source, sourceOffset, _blocks![blockIndex], blockOffset, toCopy);

            sourceOffset += toCopy;
            destinationPosition += toCopy;
            remaining -= toCopy;
        }
    }

    private void ReturnSegmentedBlocks()
    {
        if (_blocks is null)
        {
            return;
        }

        for (var i = 0; i < _blocks.Count; i++)
        {
            _arrayPool.Return(_blocks[i]);
        }

        _blocks.Clear();
    }

    private void ReturnPooledBuffers()
    {
        if (_mode == StorageMode.Segmented)
        {
            ReturnSegmentedBlocks();
            _blocks = null;
        }

        if (_mode == StorageMode.Contiguous && _contiguousBuffer is not null)
        {
            _arrayPool.Return(_contiguousBuffer);
            _contiguousBuffer = null;
        }

        for (var i = 0; i < _detachedExposedBuffers.Count; i++)
        {
            _arrayPool.Return(_detachedExposedBuffers[i]);
        }

        _detachedExposedBuffers.Clear();
    }

    private static void ValidateReadWriteBufferArguments(byte[] buffer, int offset, int count)
    {
        ThrowHelper.ThrowIfNull(buffer, nameof(buffer));
        ThrowHelper.ThrowIfNegative(offset, nameof(offset));
        ThrowHelper.ThrowIfNegative(count, nameof(count));
        if (buffer.Length - offset < count)
        {
            throw new ArgumentException("Offset and length are out of bounds.");
        }
    }
}
