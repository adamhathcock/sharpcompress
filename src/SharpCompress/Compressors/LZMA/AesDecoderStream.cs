using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA.Utilities;

namespace SharpCompress.Compressors.LZMA;

internal sealed partial class AesDecoderStream : DecoderStream2
{
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
        if (password is null)
        {
            throw new SharpCompress.Common.CryptographicException(
                "Encrypted 7Zip archive has no password specified."
            );
        }

        mStream = input;
        mLimit = limit;

        if (((uint)input.Length & 15) != 0)
        {
            throw new NotSupportedException("AES decoder does not support padding.");
        }

        Init(info, out var numCyclesPower, out var salt, out var seed);

        var passwordBytes = Encoding.Unicode.GetBytes(password);
        var key = SevenZipAesKey.Derive(numCyclesPower, salt, passwordBytes);
        if (key is null)
        {
            throw new ArchiveOperationException("Initialized with null key");
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
                    throw new IncompleteArchiveException("Unexpected end of stream.");
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
        if (info.Length == 0)
        {
            throw new InvalidFormatException("Missing 7z AES properties.");
        }
        var bt = info[0];
        numCyclesPower = bt & 0x3F;

        // Check before the no-salt/no-IV return, and before starting expensive key derivation.
        // 0x3F is the format's direct-key mode, not an iteration exponent.
        if (numCyclesPower > 24 && numCyclesPower != 0x3F)
        {
            throw new NotSupportedException("Unsupported 7z AES KDF count.");
        }

        if ((bt & 0xC0) == 0)
        {
            salt = Array.Empty<byte>();
            iv = new byte[16];
            return;
        }

        var saltSize = (bt >> 7) & 1;
        var ivSize = (bt >> 6) & 1;
        if (info.Length == 1)
        {
            throw new ArchiveOperationException();
        }

        var bt2 = info[1];
        saltSize += (bt2 >> 4);
        ivSize += (bt2 & 15);
        if (info.Length < 2 + saltSize + ivSize)
        {
            throw new ArchiveOperationException();
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

    #endregion
}
