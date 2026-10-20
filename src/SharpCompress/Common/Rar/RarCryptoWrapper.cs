using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Rar;

internal sealed class RarCryptoWrapper : Stream
{
    private readonly Stream _actualStream;
    private readonly BlockTransformer _rijndael;
    private byte[] _buffer = new byte[16];
    private int _offset;
    private int _available;

    public RarCryptoWrapper(Stream actualStream, byte[] salt, ICryptKey key)
    {
        _actualStream = actualStream;
        _rijndael = new BlockTransformer(key.Transformer(salt));
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAndDecrypt(buffer, offset, count);

    public int ReadAndDecrypt(byte[] buffer, int offset, int count)
    {
        var destination = buffer.AsSpan(offset, count);
        if (count == 1 && _available > 0)
        {
            destination[0] = _buffer[_offset++];
            _available--;
            return 1;
        }
        return ReadAndDecrypt(destination);
    }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    public override int Read(Span<byte> buffer) => ReadAndDecrypt(buffer);
#endif

    private int ReadAndDecrypt(Span<byte> buffer)
    {
        var count = buffer.Length;
        while (!buffer.IsEmpty)
        {
            if (_available == 0)
            {
                // Round only the requested bytes to an AES block; never read ahead
                // into the next entry. The buffer bounds memory independently of read size.
                var alignedSize = PrepareBuffer(buffer.Length);
                if (!_actualStream.ReadFully(_buffer.AsSpan(0, alignedSize)))
                {
                    throw new InvalidFormatException("Unexpected end of encrypted stream");
                }
                _rijndael.ProcessBlocks(_buffer, alignedSize);
                _offset = 0;
                _available = alignedSize;
            }
            var length = Math.Min(buffer.Length, _available);
            _buffer.AsSpan(_offset, length).CopyTo(buffer);
            buffer = buffer.Slice(length);
            _offset += length;
            _available -= length;
        }
        return count;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destination = buffer.AsMemory(offset, count);
        // Most small reads can complete without entering the async state machine.
        if (count <= _available)
        {
            CopyBuffered(destination.Span);
            return Task.FromResult(count);
        }
        return ReadAndDecryptAsync(destination, cancellationToken).AsTask();
    }

    private void CopyBuffered(Span<byte> destination)
    {
        if (destination.Length == 1)
        {
            destination[0] = _buffer[_offset];
        }
        else
        {
            _buffer.AsSpan(_offset, destination.Length).CopyTo(destination);
        }
        _offset += destination.Length;
        _available -= destination.Length;
    }

    private int PrepareBuffer(int count)
    {
        var alignedSize = (Math.Min(count, 8192) + 15) & ~15;
        if (_buffer.Length < alignedSize)
        {
            // Keep tiny entries cheap, growing only when callers request bulk reads.
            Array.Clear(_buffer, 0, _buffer.Length);
            _buffer = new byte[alignedSize];
        }
        return alignedSize;
    }

    private async ValueTask<int> ReadAndDecryptAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = buffer.Length;
        while (!buffer.IsEmpty)
        {
            if (_available == 0)
            {
                var alignedSize = PrepareBuffer(buffer.Length);
                try
                {
                    await _actualStream
                        .ReadExactAsync(_buffer, 0, alignedSize, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (IncompleteArchiveException e)
                {
                    throw new InvalidFormatException("Unexpected end of encrypted stream", e);
                }
                _rijndael.ProcessBlocks(_buffer, alignedSize);
                _offset = 0;
                _available = alignedSize;
            }
            var length = Math.Min(buffer.Length, _available);
            _buffer.AsMemory(_offset, length).CopyTo(buffer);
            buffer = buffer.Slice(length);
            _offset += length;
            _available -= length;
        }
        return count;
    }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.Length <= _available)
        {
            CopyBuffered(buffer.Span);
            return new ValueTask<int>(buffer.Length);
        }
        return ReadAndDecryptAsync(buffer, cancellationToken);
    }
#endif

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get; set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rijndael.Dispose();
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        base.Dispose(disposing);
    }
}
