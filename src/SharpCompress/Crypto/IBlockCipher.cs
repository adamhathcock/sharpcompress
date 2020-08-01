using System;

namespace SharpCompress.Crypto
{
    /// <remarks>Base interface for a symmetric key block cipher.</remarks>
    public interface IBlockCipher
    {
        /// <summary>The name of the algorithm this cipher implements.</summary>
        string AlgorithmName { get; }

        /// <summary>Initialise the cipher.</summary>
        /// <param name="forEncryption">Initialise for encryption if true, for decryption if false.</param>
        /// <param name="parameters">The key or other data required by the cipher.</param>
        void Init(bool forEncryption, ICipherParameters parameters);

        /// <returns>The block size for this cipher, in bytes.</returns>
        int GetBlockSize();

        /// <summary>Indicates whether this cipher can handle partial blocks.</summary>
        bool IsPartialBlockOkay { get; }

        /// <summary>Process a block.</summary>
        /// <param name="inBuf">The input buffer.</param>
        /// <param name="outBuf">The output buffer.</param>
        /// <exception cref="DataLengthException">If input block is wrong size, or outBuf too small.</exception>
        /// <returns>The number of bytes processed and produced.</returns>
        int ProcessBlock(ReadOnlySpan<byte> inBuf, Span<byte> outBuf);

        /// <summary>
        /// Reset the cipher to the same state as it was after the last init (if there was one).
        /// </summary>
        void Reset();
    }
}