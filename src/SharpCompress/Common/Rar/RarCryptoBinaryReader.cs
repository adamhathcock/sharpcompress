#nullable disable

using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Rar;

internal sealed class RarCryptoBinaryReader : RarCrcBinaryReader
{
    private BlockTransformer _rijndael;
    private readonly Queue<byte> _data = new();
    private long _readCount;

    public RarCryptoBinaryReader(Stream stream, ICryptKey cryptKey)
        : base(stream)
    {
        var salt = base.ReadBytes(EncryptionConstV5.SIZE_SALT30);
        _readCount += EncryptionConstV5.SIZE_SALT30;
        _rijndael = new BlockTransformer(cryptKey.Transformer(salt));
    }

    public RarCryptoBinaryReader(Stream stream, ICryptKey cryptKey, byte[] salt)
        : base(stream) => _rijndael = new BlockTransformer(cryptKey.Transformer(salt));

    // track read count ourselves rather than using the underlying stream since we buffer
    public override long CurrentReadByteCount
    {
        get => _readCount;
        protected set
        {
            // ignore
        }
    }

    public override void Mark() => _readCount = 0;

    public override byte ReadByte() => ReadAndDecryptBytes(1)[0];

    public override byte[] ReadBytes(int count) => ReadAndDecryptBytes(count);

    private byte[] ReadAndDecryptBytes(int count)
    {
        var queueSize = _data.Count;
        var sizeToRead = count - queueSize;

        if (sizeToRead > 0)
        {
            var alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
            for (var i = 0; i < alignedSize / 16; i++)
            {
                //long ax = System.currentTimeMillis();
                var cipherText = ReadBytesNoCrc(16);
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
