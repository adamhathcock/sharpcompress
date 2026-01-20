

using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Rar;

internal sealed class RarCryptoBinaryReader : RarCrcBinaryReader
{
    private BlockTransformer _rijndael = default!;
    private readonly Queue<byte> _data = new();
    private long _readCount;

    private RarCryptoBinaryReader(Stream stream)
        : base(stream)
    {
    }

    public static RarCryptoBinaryReader Create(Stream stream, ICryptKey cryptKey, byte[]? salt = null)
    {
        var binary = new RarCryptoBinaryReader(stream);
        if (salt == null)
        {
            salt = binary.ReadBytesBase(EncryptionConstV5.SIZE_SALT30);
            binary._readCount += EncryptionConstV5.SIZE_SALT30;
        }
        binary._rijndael = new BlockTransformer(cryptKey.Transformer(salt));
        return binary;
    }

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

    private byte[] ReadBytesBase(int count) => base.ReadBytes(count);

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
