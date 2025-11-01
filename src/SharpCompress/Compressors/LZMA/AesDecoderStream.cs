using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

internal sealed class AesDecoderStream : DecoderStream2, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => mStream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private readonly Stream mStream;
    private readonly ICryptoTransform mDecoder;
    private readonly byte[] mBuffer;
    private long mWritten;
    private readonly long mLimit;
    private int mOffset;
    private int mEnding;
    private int mUnderflow;
    private bool isDisposed;

    public AesDecoderStream(Stream input, byte[] info, IPasswordProvider pass, long limit)
    {
        var password = pass.CryptoGetTextPassword();
        if (password == null)
        {
            throw new SharpCompress.Common.CryptographicException(
                "Encrypted 7Zip archive has no password specified."
            );
        }

        mStream = input;
        mLimit = limit;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(AesDecoderStream));
#endif

        if (((uint)input.Length & 15) != 0)
        {
            throw new NotSupportedException("AES decoder does not support padding.");
        }

        Init(info, out var numCyclesPower, out var salt, out var seed);

        var passwordBytes = Encoding.Unicode.GetBytes(password);
        var key = InitKey(numCyclesPower, salt, passwordBytes);
        if (key == null)
        {
            throw new InvalidOperationException("Initialized with null key");
        }

        using (var aes = Aes.Create())
        {
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            mDecoder = aes.CreateDecryptor(key, seed);
        }

        mBuffer = new byte[4 << 10];
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
#if DEBUG_STREAMS
            this.DebugDispose(typeof(AesDecoderStream));
#endif
            if (disposing)
            {
                mStream.Dispose();
                mDecoder.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public override long Position => mWritten;

    public override long Length => mLimit;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || mWritten == mLimit)
        {
            return 0;
        }

        if (mUnderflow > 0)
        {
            return HandleUnderflow(buffer, offset, count);
        }

        // Need at least 16 bytes to proceed.
        if (mEnding - mOffset < 16)
        {
            Buffer.BlockCopy(mBuffer, mOffset, mBuffer, 0, mEnding - mOffset);
            mEnding -= mOffset;
            mOffset = 0;

            do
            {
                var read = mStream.Read(mBuffer, mEnding, mBuffer.Length - mEnding);
                if (read == 0)
                {
                    // We are not done decoding and have less than 16 bytes.
                    throw new EndOfStreamException();
                }

                mEnding += read;
            } while (mEnding - mOffset < 16);
        }

        // We shouldn't return more data than we are limited to.
        // Currently this is handled by forcing an underflow if
        // the stream length is not a multiple of the block size.
        if (count > mLimit - mWritten)
        {
            count = (int)(mLimit - mWritten);
        }

        // We cannot transform less than 16 bytes into the target buffer,
        // but we also cannot return zero, so we need to handle this.
        // We transform the data locally and use our own buffer as cache.
        if (count < 16)
        {
            return HandleUnderflow(buffer, offset, count);
        }

        if (count > mEnding - mOffset)
        {
            count = mEnding - mOffset;
        }

        // Otherwise we transform directly into the target buffer.
        var processed = mDecoder.TransformBlock(mBuffer, mOffset, count & ~15, buffer, offset);
        mOffset += processed;
        mWritten += processed;
        return processed;
    }

    #region Private Methods

    private void Init(byte[] info, out int numCyclesPower, out byte[] salt, out byte[] iv)
    {
        var bt = info[0];
        numCyclesPower = bt & 0x3F;

        if ((bt & 0xC0) == 0)
        {
            salt = Array.Empty<byte>();
            iv = Array.Empty<byte>();
            return;
        }

        var saltSize = (bt >> 7) & 1;
        var ivSize = (bt >> 6) & 1;
        if (info.Length == 1)
        {
            throw new InvalidOperationException();
        }

        var bt2 = info[1];
        saltSize += (bt2 >> 4);
        ivSize += (bt2 & 15);
        if (info.Length < 2 + saltSize + ivSize)
        {
            throw new InvalidOperationException();
        }

        salt = new byte[saltSize];
        for (var i = 0; i < saltSize; i++)
        {
            salt[i] = info[i + 2];
        }

        iv = new byte[16];
        for (var i = 0; i < ivSize; i++)
        {
            iv[i] = info[i + saltSize + 2];
        }

        if (numCyclesPower > 24)
        {
            throw new NotSupportedException();
        }
    }

    private byte[]? InitKey(int mNumCyclesPower, byte[] salt, byte[] pass)
    {
        if (mNumCyclesPower == 0x3F)
        {
            var key = new byte[32];

            int pos;
            for (pos = 0; pos < salt.Length; pos++)
            {
                key[pos] = salt[pos];
            }

            for (var i = 0; i < pass.Length && pos < 32; i++)
            {
                key[pos++] = pass[i];
            }

            return key;
        }
        else
        {
            using var sha = SHA256.Create();
            var counter = new byte[8];
            var numRounds = 1L << mNumCyclesPower;
            for (long round = 0; round < numRounds; round++)
            {
                sha.TransformBlock(salt, 0, salt.Length, null, 0);
                sha.TransformBlock(pass, 0, pass.Length, null, 0);
                sha.TransformBlock(counter, 0, 8, null, 0);

                // This mirrors the counter so we don't have to convert long to byte[] each round.
                // (It also ensures the counter is little endian, which BitConverter does not.)
                for (var i = 0; i < 8; i++)
                {
                    if (++counter[i] != 0)
                    {
                        break;
                    }
                }
            }

            sha.TransformFinalBlock(counter, 0, 0);
            return sha.Hash;
        }
    }

    private int HandleUnderflow(byte[] buffer, int offset, int count)
    {
        // If this is zero we were called to create a new underflow buffer.
        // Just transform as much as possible so we can feed from it as long as possible.
        if (mUnderflow == 0)
        {
            var blockSize = (mEnding - mOffset) & ~15;
            mUnderflow = mDecoder.TransformBlock(mBuffer, mOffset, blockSize, mBuffer, mOffset);
        }

        if (count > mUnderflow)
        {
            count = mUnderflow;
        }

        Buffer.BlockCopy(mBuffer, mOffset, buffer, offset, count);
        mWritten += count;
        mOffset += count;
        mUnderflow -= count;
        return count;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        if (count == 0 || mWritten == mLimit)
        {
            return 0;
        }

        if (mUnderflow > 0)
        {
            return HandleUnderflow(buffer, offset, count);
        }

        // Need at least 16 bytes to proceed.
        if (mEnding - mOffset < 16)
        {
            Buffer.BlockCopy(mBuffer, mOffset, mBuffer, 0, mEnding - mOffset);
            mEnding -= mOffset;
            mOffset = 0;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await mStream
                    .ReadAsync(mBuffer, mEnding, mBuffer.Length - mEnding, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    // We are not done decoding and have less than 16 bytes.
                    throw new EndOfStreamException();
                }

                mEnding += read;
            } while (mEnding - mOffset < 16);
        }

        // We shouldn't return more data than we are limited to.
        if (count > mLimit - mWritten)
        {
            count = (int)(mLimit - mWritten);
        }

        // We cannot transform less than 16 bytes into the target buffer,
        // but we also cannot return zero, so we need to handle this.
        if (count < 16)
        {
            return HandleUnderflow(buffer, offset, count);
        }

        if (count > mEnding - mOffset)
        {
            count = mEnding - mOffset;
        }

        // Otherwise we transform directly into the target buffer.
        var processed = mDecoder.TransformBlock(mBuffer, mOffset, count & ~15, buffer, offset);
        mOffset += processed;
        mWritten += processed;
        return processed;
    }

    #endregion
}
