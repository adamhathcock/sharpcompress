using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Rar;

internal sealed class RarCryptoWrapper : Stream
{
    private readonly Stream _actualStream;
    private BlockTransformer _rijndael;
    private readonly Queue<byte> _data = new();

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
        var queueSize = _data.Count;
        var sizeToRead = count - queueSize;

        if (sizeToRead > 0)
        {
            var alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
            Span<byte> cipherText = stackalloc byte[16];
            for (var i = 0; i < alignedSize / 16; i++)
            {
                //long ax = System.currentTimeMillis();
                _actualStream.Read(cipherText);

                var readBytes = _rijndael.ProcessBlock(cipherText);
                foreach (var readByte in readBytes)
                {
                    _data.Enqueue(readByte);
                }
            }

            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = _data.Dequeue();
            }
        }
        return count;
    }

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
        }

        base.Dispose(disposing);
    }
}
