#nullable disable

using System;
using System.Security.Cryptography;

namespace SharpCompress.Crypto;

internal class BlockTransformer(ICryptoTransform transformer) : IDisposable
{
    public byte[] ProcessBlock(ReadOnlySpan<byte> cipherText)
    {
        var decryptedBytes = new byte[cipherText.Length];
        transformer.TransformBlock(cipherText.ToArray(), 0, cipherText.Length, decryptedBytes, 0);

        return decryptedBytes;
    }

    public void Dispose() { }
}
