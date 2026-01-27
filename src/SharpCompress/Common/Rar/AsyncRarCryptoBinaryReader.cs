using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Rar;

internal sealed class AsyncRarCryptoBinaryReader : AsyncRarCrcBinaryReader
{
    private BlockTransformer _rijndael = default!;
    private readonly Queue<byte> _data = new();
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
            salt = await binary.ReadBytesAsyncBase(EncryptionConstV5.SIZE_SALT30);
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
        var bytes = await ReadAndDecryptBytesAsync(1, cancellationToken).ConfigureAwait(false);
        return bytes[0];
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
        var queueSize = _data.Count;
        var sizeToRead = count - queueSize;

        if (sizeToRead > 0)
        {
            var alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
            for (var i = 0; i < alignedSize / 16; i++)
            {
                var cipherText = await ReadBytesNoCrcAsync(16, cancellationToken)
                    .ConfigureAwait(false);
                var readBytes = _rijndael.ProcessBlock(cipherText);
                foreach (var readByte in readBytes)
                {
                    _data.Enqueue(readByte);
                }
            }
        }

        var decryptedBytes = new byte[count];

        for (var i = 0; i < count; i++)
        {
            var b = _data.Dequeue();
            decryptedBytes[i] = b;
            UpdateCrc(b);
        }

        _readCount += count;
        return decryptedBytes;
    }

    public void ClearQueue() => _data.Clear();

    public void SkipQueue()
    {
        var position = BaseStream.Position;
        BaseStream.Position = position + _data.Count;
        ClearQueue();
    }
}
