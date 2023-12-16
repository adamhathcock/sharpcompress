#nullable disable

using System;
using System.Security.Cryptography;

namespace SharpCompress.Crypto;

internal class BlockTransformer : IDisposable
{
    private ICryptoTransform _transform;

    public BlockTransformer(ICryptoTransform transformer)
    {
        _transform = transformer;
    }

    public byte[] ProcessBlock(ReadOnlySpan<byte> cipherText)
    {
        var decryptedBytes = new byte[cipherText.Length];
        var bytes = _transform.TransformBlock(
            cipherText.ToArray(),
            0,
            cipherText.Length,
            decryptedBytes,
            0
        );

        return decryptedBytes;
    }

    public void Dispose() { }
}
