using System;
using System.Security.Cryptography;

namespace SharpCompress.Crypto;

internal class BlockTransformer(ICryptoTransform transformer) : IDisposable
{
    // AES supports in-place transformation. Callers retain their own bounded buffers
    // instead of allocating input and output arrays for every 16-byte block.
    public void ProcessBlocks(byte[] buffer, int count) =>
        transformer.TransformBlock(buffer, 0, count, buffer, 0);

    public void Dispose() => transformer.Dispose();
}
