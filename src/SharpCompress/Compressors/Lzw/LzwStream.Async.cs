using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Lzw;

public partial class LzwStream
{
    /// <summary>
    /// Asynchronously checks if the stream is an LZW stream
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the stream is an LZW stream, false otherwise</returns>
    public static async ValueTask<bool> IsLzwStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            byte[] hdr = new byte[LzwConstants.HDR_SIZE];

            int result = await stream
                .ReadAsync(hdr, 0, hdr.Length, cancellationToken)
                .ConfigureAwait(false);

            // Check the magic marker
            if (result < 0)
            {
                throw new IncompleteArchiveException("Failed to read LZW header");
            }

            if (hdr[0] != (LzwConstants.MAGIC >> 8) || hdr[1] != (LzwConstants.MAGIC & 0xff))
            {
                throw new IncompleteArchiveException(
                    String.Format(
                        Constants.DefaultCultureInfo,
                        "Wrong LZW header. Magic bytes don't match. 0x{0:x2} 0x{1:x2}",
                        hdr[0],
                        hdr[1]
                    )
                );
            }
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Reads decompressed data asynchronously into the provided buffer byte array
    /// </summary>
    /// <param name="buffer">The array to read and decompress data into</param>
    /// <param name="offset">The offset indicating where the data should be placed</param>
    /// <param name="count">The number of bytes to decompress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of bytes read. Zero signals the end of stream</returns>
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (!headerParsed)
        {
            await ParseHeaderAsync(cancellationToken).ConfigureAwait(false);
        }

        if (eof)
        {
            return 0;
        }

        int start = offset;

        int[] lTabPrefix = tabPrefix;
        byte[] lTabSuffix = tabSuffix;
        byte[] lStack = stack;
        int lNBits = nBits;
        int lMaxCode = maxCode;
        int lMaxMaxCode = maxMaxCode;
        int lBitMask = bitMask;
        int lOldCode = oldCode;
        byte lFinChar = finChar;
        int lStackP = stackP;
        int lFreeEnt = freeEnt;
        byte[] lData = data;
        int lBitPos = bitPos;

        int sSize = lStack.Length - lStackP;
        if (sSize > 0)
        {
            int num = (sSize >= count) ? count : sSize;
            Array.Copy(lStack, lStackP, buffer, offset, num);
            offset += num;
            count -= num;
            lStackP += num;
        }

        if (count == 0)
        {
            stackP = lStackP;
            return offset - start;
        }

        MainLoop:
        do
        {
            if (end < EXTRA)
            {
                await FillAsync(cancellationToken).ConfigureAwait(false);
            }

            int bitIn = (got > 0) ? (end - end % lNBits) << 3 : (end << 3) - (lNBits - 1);

            while (lBitPos < bitIn)
            {
                if (count == 0)
                {
                    nBits = lNBits;
                    maxCode = lMaxCode;
                    maxMaxCode = lMaxMaxCode;
                    bitMask = lBitMask;
                    oldCode = lOldCode;
                    finChar = lFinChar;
                    stackP = lStackP;
                    freeEnt = lFreeEnt;
                    bitPos = lBitPos;

                    return offset - start;
                }

                if (lFreeEnt > lMaxCode)
                {
                    int nBytes = lNBits << 3;
                    lBitPos = (lBitPos - 1) + nBytes - (lBitPos - 1 + nBytes) % nBytes;

                    lNBits++;
                    lMaxCode = (lNBits == maxBits) ? lMaxMaxCode : (1 << lNBits) - 1;

                    lBitMask = (1 << lNBits) - 1;
                    lBitPos = ResetBuf(lBitPos);
                    goto MainLoop;
                }

                int pos = lBitPos >> 3;
                int code =
                    (
                        (
                            (lData[pos] & 0xFF)
                            | ((lData[pos + 1] & 0xFF) << 8)
                            | ((lData[pos + 2] & 0xFF) << 16)
                        ) >> (lBitPos & 0x7)
                    ) & lBitMask;

                lBitPos += lNBits;

                if (lOldCode == -1)
                {
                    if (code >= 256)
                    {
                        throw new IncompleteArchiveException("corrupt input: " + code + " > 255");
                    }

                    lFinChar = (byte)(lOldCode = code);
                    buffer[offset++] = lFinChar;
                    count--;
                    continue;
                }

                if (code == TBL_CLEAR && blockMode)
                {
                    Array.Copy(zeros, 0, lTabPrefix, 0, zeros.Length);
                    lFreeEnt = TBL_FIRST - 1;

                    int nBytes = lNBits << 3;
                    lBitPos = (lBitPos - 1) + nBytes - (lBitPos - 1 + nBytes) % nBytes;
                    lNBits = LzwConstants.INIT_BITS;
                    lMaxCode = (1 << lNBits) - 1;
                    lBitMask = lMaxCode;

                    lBitPos = ResetBuf(lBitPos);
                    goto MainLoop;
                }

                int inCode = code;
                lStackP = lStack.Length;

                if (code >= lFreeEnt)
                {
                    if (code > lFreeEnt)
                    {
                        throw new IncompleteArchiveException(
                            "corrupt input: code=" + code + ", freeEnt=" + lFreeEnt
                        );
                    }

                    lStack[--lStackP] = lFinChar;
                    code = lOldCode;
                }

                while (code >= 256)
                {
                    lStack[--lStackP] = lTabSuffix[code];
                    code = lTabPrefix[code];
                }

                lFinChar = lTabSuffix[code];
                buffer[offset++] = lFinChar;
                count--;

                sSize = lStack.Length - lStackP;
                int num = (sSize >= count) ? count : sSize;
                Array.Copy(lStack, lStackP, buffer, offset, num);
                offset += num;
                count -= num;
                lStackP += num;

                if (lFreeEnt < lMaxMaxCode)
                {
                    lTabPrefix[lFreeEnt] = lOldCode;
                    lTabSuffix[lFreeEnt] = lFinChar;
                    lFreeEnt++;
                }

                lOldCode = inCode;

                if (count == 0)
                {
                    nBits = lNBits;
                    maxCode = lMaxCode;
                    bitMask = lBitMask;
                    oldCode = lOldCode;
                    finChar = lFinChar;
                    stackP = lStackP;
                    freeEnt = lFreeEnt;
                    bitPos = lBitPos;

                    return offset - start;
                }
            }

            lBitPos = ResetBuf(lBitPos);
        } while (got > 0);

        nBits = lNBits;
        maxCode = lMaxCode;
        bitMask = lBitMask;
        oldCode = lOldCode;
        finChar = lFinChar;
        stackP = lStackP;
        freeEnt = lFreeEnt;
        bitPos = lBitPos;

        eof = true;
        return offset - start;
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Reads decompressed data asynchronously into the provided buffer
    /// </summary>
    /// <param name="buffer">The memory to read and decompress data into</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of bytes read. Zero signals the end of stream</returns>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        byte[] array = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int read = await ReadAsync(array, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            array.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(array);
        }
    }
#endif

    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        got = await baseInputStream
            .ReadAsync(data, end, data.Length - 1 - end, cancellationToken)
            .ConfigureAwait(false);
        if (got > 0)
        {
            end += got;
        }
    }

    private async ValueTask ParseHeaderAsync(CancellationToken cancellationToken)
    {
        headerParsed = true;

        byte[] hdr = new byte[LzwConstants.HDR_SIZE];

        int result = await baseInputStream
            .ReadAsync(hdr, 0, hdr.Length, cancellationToken)
            .ConfigureAwait(false);

        if (result < 0)
        {
            throw new IncompleteArchiveException("Failed to read LZW header");
        }

        if (hdr[0] != (LzwConstants.MAGIC >> 8) || hdr[1] != (LzwConstants.MAGIC & 0xff))
        {
            throw new IncompleteArchiveException(
                String.Format(
                    Constants.DefaultCultureInfo,
                    "Wrong LZW header. Magic bytes don't match. 0x{0:x2} 0x{1:x2}",
                    hdr[0],
                    hdr[1]
                )
            );
        }

        blockMode = (hdr[2] & LzwConstants.BLOCK_MODE_MASK) > 0;
        maxBits = hdr[2] & LzwConstants.BIT_MASK;

        if (maxBits > LzwConstants.MAX_BITS)
        {
            throw new ArchiveException(
                "Stream compressed with "
                    + maxBits
                    + " bits, but decompression can only handle "
                    + LzwConstants.MAX_BITS
                    + " bits."
            );
        }

        if ((hdr[2] & LzwConstants.RESERVED_MASK) > 0)
        {
            throw new ArchiveException("Unsupported bits set in the header.");
        }

        maxMaxCode = 1 << maxBits;
        nBits = LzwConstants.INIT_BITS;
        maxCode = (1 << nBits) - 1;
        bitMask = maxCode;
        oldCode = -1;
        finChar = 0;
        freeEnt = blockMode ? TBL_FIRST : 256;

        tabPrefix = new int[1 << maxBits];
        tabSuffix = new byte[1 << maxBits];
        stack = new byte[1 << maxBits];
        stackP = stack.Length;

        for (int idx = 255; idx >= 0; idx--)
        {
            tabSuffix[idx] = (byte)idx;
        }
    }
}
