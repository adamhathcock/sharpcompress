using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal sealed class AsyncRarCryptoBinaryReader : AsyncRarCrcBinaryReader
{
    private BlockTransformer _rijndael = default!;
    private readonly byte[] _block = new byte[16];
    private int _blockOffset = 16;
    private long _readCount;

    private AsyncRarCryptoBinaryReader(Stream stream)
        : base(stream) { }

    public static async ValueTask<AsyncRarCryptoBinaryReader> Create(
        Stream stream,
        ICryptKey cryptKey,
        byte[]? salt = null
    )
    {
        var binary = new AsyncRarCryptoBinaryReader(stream);
        if (salt == null)
        {
            salt = await binary
                .ReadBytesAsyncBase(EncryptionConstV5.SIZE_SALT30)
                .ConfigureAwait(false);
            binary._readCount += EncryptionConstV5.SIZE_SALT30;
        }
        binary._rijndael = new BlockTransformer(cryptKey.Transformer(salt));
        return binary;
    }

    public override long CurrentReadByteCount
    {
        get => _readCount;
        protected set
        {
            // ignore
        }
    }

    public override void Mark() => _readCount = 0;

    public override async ValueTask<byte> ReadByteAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_blockOffset == _block.Length)
        {
            try
            {
                await BaseStream
                    .ReadExactAsync(_block, 0, _block.Length, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IncompleteArchiveException e)
            {
                throw new InvalidFormatException("Unexpected end of encrypted stream", e);
            }
            _rijndael.ProcessBlocks(_block, _block.Length);
            _blockOffset = 0;
        }
        var value = _block[_blockOffset++];
        UpdateCrc(value);
        _readCount++;
        return value;
    }

    private ValueTask<byte[]> ReadBytesAsyncBase(int count) => base.ReadBytesAsync(count);

    public override async ValueTask<byte[]> ReadBytesAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        return await ReadAndDecryptBytesAsync(count, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<byte[]> ReadAndDecryptBytesAsync(
        int count,
        CancellationToken cancellationToken
    )
    {
        var decryptedBytes = new byte[count];

        for (var i = 0; i < count; i++)
        {
            decryptedBytes[i] = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        }

        return decryptedBytes;
    }

    public void ClearQueue() => _blockOffset = _block.Length;

    public void SkipQueue()
    {
        var position = BaseStream.Position;
        BaseStream.Position = position + _block.Length - _blockOffset;
        ClearQueue();
    }

    public override void Dispose()
    {
        _rijndael.Dispose();
        base.Dispose();
    }

#if NET8_0_OR_GREATER
    public override ValueTask DisposeAsync()
    {
        _rijndael.Dispose();
        return base.DisposeAsync();
    }
#endif
}
