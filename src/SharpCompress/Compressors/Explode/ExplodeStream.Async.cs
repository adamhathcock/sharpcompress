using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Explode;

public partial class ExplodeStream
{
    internal static async ValueTask<ExplodeStream> CreateAsync(
        Stream inStr,
        long compressedSize,
        long uncompressedSize,
        HeaderFlags generalPurposeBitFlag,
        CancellationToken cancellationToken = default
    )
    {
        var ex = new ExplodeStream(inStr, compressedSize, uncompressedSize, generalPurposeBitFlag);
        await ex.explode_SetTables_async(cancellationToken).ConfigureAwait(false);
        ex.explode_var_init();
        return ex;
    }

    private async Task<int> get_tree_async(
        int[] arrBitLengths,
        int numberExpected,
        CancellationToken cancellationToken
    )
    {
        int inIndex = (await ReadSingleByteAsync(cancellationToken).ConfigureAwait(false)) + 1;
        int outIndex = 0;
        do
        {
            int nextByte = await ReadSingleByteAsync(cancellationToken).ConfigureAwait(false);
            int bitLengthOfCodes = (nextByte & 0xf) + 1;
            int numOfCodes = ((nextByte & 0xf0) >> 4) + 1;
            if (outIndex + numOfCodes > numberExpected)
            {
                return 4;
            }

            do
            {
                arrBitLengths[outIndex++] = bitLengthOfCodes;
            } while ((--numOfCodes) != 0);
        } while ((--inIndex) != 0);

        return outIndex != numberExpected ? 4 : 0;
    }

    private async Task<int> ReadSingleByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        int bytesRead = await inStream
            .ReadAsync(buffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead == 0)
        {
            return -1;
        }
        return buffer[0];
    }

    private async Task<int> explode_SetTables_async(CancellationToken cancellationToken)
    {
        int returnCode;
        int[] arrBitLengthsForCodes = new int[256];

        bitsForLiteralCodeTable = 0;
        bitsForLengthCodeTable = 7;
        bitsForDistanceCodeTable = (compressedSize) > 200000 ? 8 : 7;

        if ((generalPurposeBitFlag & HeaderFlags.Bit2) != 0)
        {
            bitsForLiteralCodeTable = 9;
            if (
                (
                    returnCode = await get_tree_async(arrBitLengthsForCodes, 256, cancellationToken)
                        .ConfigureAwait(false)
                ) != 0
            )
            {
                return returnCode;
            }

            if (
                (
                    returnCode = HuftTree.huftbuid(
                        arrBitLengthsForCodes,
                        256,
                        256,
                        [],
                        [],
                        out hufLiteralCodeTable,
                        ref bitsForLiteralCodeTable
                    )
                ) != 0
            )
            {
                return returnCode;
            }

            if (
                (
                    returnCode = await get_tree_async(arrBitLengthsForCodes, 64, cancellationToken)
                        .ConfigureAwait(false)
                ) != 0
            )
            {
                return returnCode;
            }

            if (
                (
                    returnCode = HuftTree.huftbuid(
                        arrBitLengthsForCodes,
                        64,
                        0,
                        cplen3,
                        extra,
                        out hufLengthCodeTable,
                        ref bitsForLengthCodeTable
                    )
                ) != 0
            )
            {
                return returnCode;
            }
        }
        else
        {
            if (
                (
                    returnCode = await get_tree_async(arrBitLengthsForCodes, 64, cancellationToken)
                        .ConfigureAwait(false)
                ) != 0
            )
            {
                return returnCode;
            }

            hufLiteralCodeTable = null;

            if (
                (
                    returnCode = HuftTree.huftbuid(
                        arrBitLengthsForCodes,
                        64,
                        0,
                        cplen2,
                        extra,
                        out hufLengthCodeTable,
                        ref bitsForLengthCodeTable
                    )
                ) != 0
            )
            {
                return returnCode;
            }
        }

        if (
            (
                returnCode = await get_tree_async(arrBitLengthsForCodes, 64, cancellationToken)
                    .ConfigureAwait(false)
            ) != 0
        )
        {
            return (int)returnCode;
        }

        if ((generalPurposeBitFlag & HeaderFlags.Bit1) != 0)
        {
            numOfUncodedLowerDistanceBits = 7;
            returnCode = HuftTree.huftbuid(
                arrBitLengthsForCodes,
                64,
                0,
                cpdist8,
                extra,
                out hufDistanceCodeTable,
                ref bitsForDistanceCodeTable
            );
        }
        else
        {
            numOfUncodedLowerDistanceBits = 6;
            returnCode = HuftTree.huftbuid(
                arrBitLengthsForCodes,
                64,
                0,
                cpdist4,
                extra,
                out hufDistanceCodeTable,
                ref bitsForDistanceCodeTable
            );
        }

        return returnCode;
    }

    private async Task NeedBitsAsync(int numberOfBits, CancellationToken cancellationToken)
    {
        while (bitBufferCount < (numberOfBits))
        {
            int byteRead = await ReadSingleByteAsync(cancellationToken).ConfigureAwait(false);
            bitBuffer |= (uint)byteRead << bitBufferCount;
            bitBufferCount += 8;
        }
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
                await NeedBitsAsync(1, cancellationToken).ConfigureAwait(false);
                bool literal = (bitBuffer & 1) == 1;
                DumpBits(1);

                huftNode huftPointer;
                if (literal)
                {
                    byte nextByte;
                    if (hufLiteralCodeTable != null)
                    {
                        if (
                            DecodeHuft(
                                hufLiteralCodeTable,
                                bitsForLiteralCodeTable,
                                maskForLiteralCodeTable,
                                out huftPointer,
                                out _
                            ) != 0
                        )
                        {
                            throw new Exception("Error decoding literal value");
                        }

                        nextByte = (byte)huftPointer.Value;
                    }
                    else
                    {
                        await NeedBitsAsync(8, cancellationToken).ConfigureAwait(false);
                        nextByte = (byte)bitBuffer;
                        DumpBits(8);
                    }

                    buffer[offset + (countIndex++)] = nextByte;
                    windowsBuffer[windowIndex++] = nextByte;
                    outBytesCount++;

                    if (windowIndex == WSIZE)
                    {
                        windowIndex = 0;
                    }

                    continue;
                }

                await NeedBitsAsync(numOfUncodedLowerDistanceBits, cancellationToken)
                    .ConfigureAwait(false);
                distance = (int)(bitBuffer & maskForDistanceLowBits);
                DumpBits(numOfUncodedLowerDistanceBits);

                if (
                    DecodeHuft(
                        hufDistanceCodeTable,
                        bitsForDistanceCodeTable,
                        maskForDistanceCodeTable,
                        out huftPointer,
                        out _
                    ) != 0
                )
                {
                    throw new Exception("Error decoding distance high bits");
                }

                distance = windowIndex - (distance + huftPointer.Value);

                if (
                    DecodeHuft(
                        hufLengthCodeTable,
                        bitsForLengthCodeTable,
                        maskForLengthCodeTable,
                        out huftPointer,
                        out int extraBitLength
                    ) != 0
                )
                {
                    throw new Exception("Error decoding coded length");
                }

                length = huftPointer.Value;

                if (extraBitLength != 0)
                {
                    await NeedBitsAsync(8, cancellationToken).ConfigureAwait(false);
                    length += (int)(bitBuffer & 0xff);
                    DumpBits(8);
                }

                if (length > (unCompressedSize - outBytesCount))
                {
                    length = (int)(unCompressedSize - outBytesCount);
                }

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
