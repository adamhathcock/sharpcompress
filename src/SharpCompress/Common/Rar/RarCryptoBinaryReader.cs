using System.IO;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal sealed class RarCryptoBinaryReader : RarCrcBinaryReader
{
    private BlockTransformer _rijndael = default!;
    private readonly byte[] _block = new byte[16];
    private int _blockOffset = 16;
    private long _readCount;

    private RarCryptoBinaryReader(Stream stream)
        : base(stream) { }

    public static RarCryptoBinaryReader Create(
        Stream stream,
        ICryptKey cryptKey,
        byte[]? salt = null
    )
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

    public override byte ReadByte()
    {
        if (_blockOffset == _block.Length)
        {
            if (!BaseStream.ReadFully(_block))
            {
                throw new InvalidFormatException("Unexpected end of encrypted stream");
            }
            _rijndael.ProcessBlocks(_block, _block.Length);
            _blockOffset = 0;
        }
        var value = _block[_blockOffset++];
        UpdateCrc(value);
        _readCount++;
        return value;
    }

    public override byte[] ReadBytes(int count) => ReadAndDecryptBytes(count);

    private byte[] ReadBytesBase(int count) => base.ReadBytes(count);

    private byte[] ReadAndDecryptBytes(int count)
    {
        var decryptedBytes = new byte[count];

        for (var i = 0; i < count; i++)
        {
            decryptedBytes[i] = ReadByte();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rijndael.Dispose();
        }
        base.Dispose(disposing);
    }
}
