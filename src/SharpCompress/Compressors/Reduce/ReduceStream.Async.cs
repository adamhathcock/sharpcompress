using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Reduce;

public partial class ReduceStream
{
    public static async ValueTask<ReduceStream> CreateAsync(
        Stream inStr,
        long compsize,
        long unCompSize,
        int factor,
        CancellationToken cancellationToken = default
    )
    {
        var stream = new ReduceStream(inStr, compsize, unCompSize, factor);
        await stream.LoadNextByteTableAsync(cancellationToken).ConfigureAwait(false);
        return stream;
    }

    private async Task<int> NEXTBYTEAsync(CancellationToken cancellationToken)
    {
        if (inByteCount == compressedSize)
        {
            return EOF;
        }

        byte[] buffer = new byte[1];
        int bytesRead = await inStream
            .ReadAsync(buffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead == 0)
        {
            return EOF;
        }

        inByteCount++;
        return buffer[0];
    }

    private async Task<byte> READBITSAsync(int nbits, CancellationToken cancellationToken)
    {
        if (nbits > bitBufferCount)
        {
            int temp;
            while (bitBufferCount <= 8 * (int)(4 - 1))
            {
                temp = await NEXTBYTEAsync(cancellationToken).ConfigureAwait(false);
                if (temp == EOF)
                {
                    break;
                }
                bitBuffer |= (ulong)temp << bitBufferCount;
                bitBufferCount += 8;
            }
        }
        byte zdest = (byte)(bitBuffer & (ulong)mask_bits[nbits]);
        bitBuffer >>= nbits;
        bitBufferCount -= nbits;
        return zdest;
    }

    private async Task LoadNextByteTableAsync(CancellationToken cancellationToken)
    {
        nextByteTable = new byte[256][];
        for (int x = 255; x >= 0; x--)
        {
            byte Slen = await READBITSAsync(6, cancellationToken).ConfigureAwait(false);
            nextByteTable[x] = new byte[Slen];
            for (int i = 0; i < Slen; i++)
            {
                nextByteTable[x][i] = await READBITSAsync(8, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<byte> GetNextByteAsync(CancellationToken cancellationToken)
    {
        if (nextByteTable[outByte].Length == 0)
        {
            outByte = await READBITSAsync(8, cancellationToken).ConfigureAwait(false);
            return outByte;
        }
        byte nextBit = await READBITSAsync(1, cancellationToken).ConfigureAwait(false);
        if (nextBit == 1)
        {
            outByte = await READBITSAsync(8, cancellationToken).ConfigureAwait(false);
            return outByte;
        }
        byte nextByteIndex = await READBITSAsync(
                bitCountTable[nextByteTable[outByte].Length],
                cancellationToken
            )
            .ConfigureAwait(false);
        outByte = nextByteTable[outByte][nextByteIndex];
        return outByte;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        int countIndex = 0;
        while (countIndex < count && outBytesCount < unCompressedSize)
        {
            if (length == 0)
            {
                byte nextByte = await GetNextByteAsync(cancellationToken).ConfigureAwait(false);
                if (nextByte != RunLengthCode)
                {
                    buffer[offset + (countIndex++)] = nextByte;
                    windowsBuffer[windowIndex++] = nextByte;
                    outBytesCount++;
                    if (windowIndex == WSIZE)
                    {
                        windowIndex = 0;
                    }

                    continue;
                }

                nextByte = await GetNextByteAsync(cancellationToken).ConfigureAwait(false);
                if (nextByte == 0)
                {
                    buffer[offset + (countIndex++)] = RunLengthCode;
                    windowsBuffer[windowIndex++] = RunLengthCode;
                    outBytesCount++;
                    if (windowIndex == WSIZE)
                    {
                        windowIndex = 0;
                    }

                    continue;
                }

                int lengthDistanceByte = nextByte;
                length = lengthDistanceByte & lengthMask;
                if (length == lengthMask)
                {
                    length += await GetNextByteAsync(cancellationToken).ConfigureAwait(false);
                }
                length += 3;

                int distanceHighByte = (lengthDistanceByte << factor) & distanceMask;
                distance =
                    windowIndex
                    - (
                        distanceHighByte
                        + await GetNextByteAsync(cancellationToken).ConfigureAwait(false)
                        + 1
                    );

                distance &= WSIZE - 1;
            }

            while (length != 0 && countIndex < count)
            {
                byte nextByte = windowsBuffer[distance++];
                buffer[offset + (countIndex++)] = nextByte;
                windowsBuffer[windowIndex++] = nextByte;
                outBytesCount++;

                if (distance == WSIZE)
                {
                    distance = 0;
                }

                if (windowIndex == WSIZE)
                {
                    windowIndex = 0;
                }

                length--;
            }
        }

        return countIndex;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || outBytesCount >= unCompressedSize)
        {
            return 0;
        }

        byte[] arrayBuffer = new byte[buffer.Length];
        int result = await ReadAsync(arrayBuffer, 0, arrayBuffer.Length, cancellationToken)
            .ConfigureAwait(false);
        arrayBuffer.AsMemory(0, result).CopyTo(buffer);
        return result;
    }
#endif
}
