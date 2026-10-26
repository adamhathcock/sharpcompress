using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA;

/// <summary>
/// Write-only 7zAES encoder: AES-256-CBC without padding mode. The final partial block is
/// zero-filled to 16 bytes by <see cref="Finish" />; readers trim it using the coder's unpack
/// size, as <see cref="AesDecoderStream" /> does. Callers must call <see cref="Finish" /> or
/// <see cref="FinishAsync" />; disposing never writes, so it can't hide an earlier exception.
/// Does not dispose the output stream.
/// </summary>
internal sealed class AesEncoderStream : Stream
{
    private const int BlockSize = 16;
    private const int BufferSize = 4 << 10;

    private readonly Stream _output;
    private readonly ICryptoTransform _encryptor;
    private readonly byte[] _pending = new byte[BlockSize];
    private readonly byte[] _buffer;
    private readonly ArrayPool<byte> _pool;
    private int _pendingCount;
    private bool _finished;
    private bool _isDisposed;

    public AesEncoderStream(Stream output, byte[] key, byte[] iv)
        : this(output, key, iv, ArrayPool<byte>.Shared) { }

    internal AesEncoderStream(Stream output, byte[] key, byte[] iv, ArrayPool<byte> pool)
    {
        _output = output;
        _pool = pool;
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        _encryptor = aes.CreateEncryptor(key, iv);
        try
        {
            // Rent only after crypto setup succeeds; failed construction must not leak a lease.
            _buffer = pool.Rent(BufferSize);
        }
        catch
        {
            _encryptor.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Bytes written to this stream before padding: the unpack size of the AES coder.
    /// </summary>
    public long InputLength { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_isDisposed && !_finished;

    public override long Length => InputLength;

    public override long Position
    {
        get => InputLength;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _output.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _output.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void WriteByte(byte value)
    {
        ThrowIfFinished();
        InputLength++;
        // The LZMA range encoder emits bytes individually. Stream's default implementation
        // allocates a new array for each byte instead of using our pending CBC block.
        _pending[_pendingCount++] = value;
        if (_pendingCount == BlockSize)
        {
            _pendingCount = 0;
            var encrypted = _encryptor.TransformBlock(_pending, 0, BlockSize, _buffer, 0);
            _output.Write(_buffer, 0, encrypted);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfFinished();
        InputLength += count;
        while (count > 0)
        {
            var encrypted = Encrypt(buffer, ref offset, ref count);
            if (encrypted > 0)
            {
                _output.Write(_buffer, 0, encrypted);
            }
        }
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ThrowIfFinished();
        InputLength += count;
        while (count > 0)
        {
            var encrypted = Encrypt(buffer, ref offset, ref count);
            if (encrypted > 0)
            {
                await _output
                    .WriteAsync(_buffer, 0, encrypted, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Pads and writes the final partial block. Must be called once all data has been written.
    /// </summary>
    public void Finish()
    {
        ThrowIfDisposed();
        var encrypted = FinishBlock();
        if (encrypted > 0)
        {
            _output.Write(_buffer, 0, encrypted);
        }
    }

    /// <summary>
    /// Asynchronously pads and writes the final partial block.
    /// </summary>
    public async ValueTask FinishAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var encrypted = FinishBlock();
        if (encrypted > 0)
        {
            await _output
                .WriteAsync(_buffer, 0, encrypted, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;
            try
            {
                _encryptor.Dispose();
            }
            finally
            {
                // The pooled scratch contains ciphertext only; pending plaintext stays private.
                Array.Clear(_pending, 0, _pending.Length);
                _pool.Return(_buffer);
            }
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Encrypts as many whole blocks as fit into <see cref="_buffer" /> from the pending block
    /// and the caller's data, advancing <paramref name="offset" /> and <paramref name="count" />.
    /// Returns the number of encrypted bytes now in <see cref="_buffer" />.
    /// </summary>
    private int Encrypt(byte[] buffer, ref int offset, ref int count)
    {
        // Complete a partially filled block first, so CBC chaining stays in input order.
        if (_pendingCount > 0 || count < BlockSize)
        {
            var take = Math.Min(BlockSize - _pendingCount, count);
            Buffer.BlockCopy(buffer, offset, _pending, _pendingCount, take);
            _pendingCount += take;
            offset += take;
            count -= take;
            if (_pendingCount < BlockSize)
            {
                return 0;
            }

            _pendingCount = 0;
            return _encryptor.TransformBlock(_pending, 0, BlockSize, _buffer, 0);
        }

        var whole = Math.Min(count & ~(BlockSize - 1), BufferSize);
        var written = _encryptor.TransformBlock(buffer, offset, whole, _buffer, 0);
        offset += whole;
        count -= whole;
        return written;
    }

    private int FinishBlock()
    {
        if (_finished)
        {
            return 0;
        }

        _finished = true;
        if (_pendingCount == 0)
        {
            return 0;
        }

        Array.Clear(_pending, _pendingCount, BlockSize - _pendingCount);
        _pendingCount = 0;
        return _encryptor.TransformBlock(_pending, 0, BlockSize, _buffer, 0);
    }

    private void ThrowIfFinished()
    {
        ThrowIfDisposed();
        if (_finished)
        {
            throw new ObjectDisposedException(
                nameof(AesEncoderStream),
                "Cannot write after the final block has been written."
            );
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AesEncoderStream));
        }
    }
}
