using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.LZMA;

internal sealed partial class AesDecoderStream
{
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
                    throw new IncompleteArchiveException("Unexpected end of stream.");
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
}
