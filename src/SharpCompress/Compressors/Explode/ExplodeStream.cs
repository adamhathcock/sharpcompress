using System;
using System.IO;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Compressors.Explode;

public class ExplodeStream : Stream
{
    private const int INVALID_CODE = 99;
    private const int WSIZE = 64 * 1024;

    private readonly long unCompressedSize;
    private readonly int compressedSize;
    private readonly HeaderFlags generalPurposeBitFlag;
    private readonly Stream inStream;

    private huftNode[]? hufLiteralCodeTable; /* literal code table */
    private huftNode[] hufLengthCodeTable = []; /* length code table */
    private huftNode[] hufDistanceCodeTable = []; /* distance code table */

    private int bitsForLiteralCodeTable;
    private int bitsForLengthCodeTable;
    private int bitsForDistanceCodeTable;
    private int numOfUncodedLowerDistanceBits; /* number of uncoded lower distance bits */

    private ulong bitBuffer;
    private int bitBufferCount;

    private readonly byte[] windowsBuffer;
    private uint maskForLiteralCodeTable;
    private uint maskForLengthCodeTable;
    private uint maskForDistanceCodeTable;
    private uint maskForDistanceLowBits;
    private long outBytesCount;

    private int windowIndex;
    private int distance;
    private int length;

    internal ExplodeStream(
        Stream inStr,
        long compressedSize,
        long uncompressedSize,
        HeaderFlags generalPurposeBitFlag
    )
    {
        inStream = inStr;
        this.compressedSize = (int)compressedSize;
        unCompressedSize = (long)uncompressedSize;
        this.generalPurposeBitFlag = generalPurposeBitFlag;
        explode_SetTables();

        windowsBuffer = new byte[WSIZE];
        explode_var_init();
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => unCompressedSize;
    public override long Position
    {
        get => outBytesCount;
        set { }
    }

    static uint[] mask_bits = new uint[]
    {
        0x0000,
        0x0001,
        0x0003,
        0x0007,
        0x000f,
        0x001f,
        0x003f,
        0x007f,
        0x00ff,
        0x01ff,
        0x03ff,
        0x07ff,
        0x0fff,
        0x1fff,
        0x3fff,
        0x7fff,
        0xffff,
    };

    /* Tables for length and distance */
    static int[] cplen2 = new int[]
    {
        2,
        3,
        4,
        5,
        6,
        7,
        8,
        9,
        10,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18,
        19,
        20,
        21,
        22,
        23,
        24,
        25,
        26,
        27,
        28,
        29,
        30,
        31,
        32,
        33,
        34,
        35,
        36,
        37,
        38,
        39,
        40,
        41,
        42,
        43,
        44,
        45,
        46,
        47,
        48,
        49,
        50,
        51,
        52,
        53,
        54,
        55,
        56,
        57,
        58,
        59,
        60,
        61,
        62,
        63,
        64,
        65,
    };

    static int[] cplen3 = new int[]
    {
        3,
        4,
        5,
        6,
        7,
        8,
        9,
        10,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18,
        19,
        20,
        21,
        22,
        23,
        24,
        25,
        26,
        27,
        28,
        29,
        30,
        31,
        32,
        33,
        34,
        35,
        36,
        37,
        38,
        39,
        40,
        41,
        42,
        43,
        44,
        45,
        46,
        47,
        48,
        49,
        50,
        51,
        52,
        53,
        54,
        55,
        56,
        57,
        58,
        59,
        60,
        61,
        62,
        63,
        64,
        65,
        66,
    };

    static int[] extra = new int[]
    {
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        8,
    };

    static int[] cpdist4 = new int[]
    {
        1,
        65,
        129,
        193,
        257,
        321,
        385,
        449,
        513,
        577,
        641,
        705,
        769,
        833,
        897,
        961,
        1025,
        1089,
        1153,
        1217,
        1281,
        1345,
        1409,
        1473,
        1537,
        1601,
        1665,
        1729,
        1793,
        1857,
        1921,
        1985,
        2049,
        2113,
        2177,
        2241,
        2305,
        2369,
        2433,
        2497,
        2561,
        2625,
        2689,
        2753,
        2817,
        2881,
        2945,
        3009,
        3073,
        3137,
        3201,
        3265,
        3329,
        3393,
        3457,
        3521,
        3585,
        3649,
        3713,
        3777,
        3841,
        3905,
        3969,
        4033,
    };

    static int[] cpdist8 = new int[]
    {
        1,
        129,
        257,
        385,
        513,
        641,
        769,
        897,
        1025,
        1153,
        1281,
        1409,
        1537,
        1665,
        1793,
        1921,
        2049,
        2177,
        2305,
        2433,
        2561,
        2689,
        2817,
        2945,
        3073,
        3201,
        3329,
        3457,
        3585,
        3713,
        3841,
        3969,
        4097,
        4225,
        4353,
        4481,
        4609,
        4737,
        4865,
        4993,
        5121,
        5249,
        5377,
        5505,
        5633,
        5761,
        5889,
        6017,
        6145,
        6273,
        6401,
        6529,
        6657,
        6785,
        6913,
        7041,
        7169,
        7297,
        7425,
        7553,
        7681,
        7809,
        7937,
        8065,
    };

    private int get_tree(int[] arrBitLengths, int numberExpected)
    /* Get the bit lengths for a code representation from the compressed
       stream.  If get_tree() returns 4, then there is an error in the data.
       Otherwise zero is returned. */
    {
        /* get bit lengths */
        int inIndex = inStream.ReadByte() + 1; /* length/count pairs to read */
        int outIndex = 0; /* next code */
        do
        {
            int nextByte = inStream.ReadByte();
            int bitLengthOfCodes = (nextByte & 0xf) + 1; /* bits in code (1..16) */
            int numOfCodes = ((nextByte & 0xf0) >> 4) + 1; /* codes with those bits (1..16) */
            if (outIndex + numOfCodes > numberExpected)
                return 4; /* don't overflow arrBitLengths[] */
            do
            {
                arrBitLengths[outIndex++] = bitLengthOfCodes;
            } while ((--numOfCodes) != 0);
        } while ((--inIndex) != 0);

        return outIndex != numberExpected ? 4 : 0; /* should have read numberExpected of them */
    }

    private int explode_SetTables()
    {
        int returnCode; /* return codes */
        int[] arrBitLengthsForCodes = new int[256]; /* bit lengths for codes */

        bitsForLiteralCodeTable = 0; /* bits for tb */
        bitsForLengthCodeTable = 7;
        bitsForDistanceCodeTable = (compressedSize) > 200000 ? 8 : 7;

        if ((generalPurposeBitFlag & HeaderFlags.Bit2) != 0)
        /* With literal tree--minimum match length is 3 */
        {
            bitsForLiteralCodeTable = 9; /* base table size for literals */
            if ((returnCode = get_tree(arrBitLengthsForCodes, 256)) != 0)
                return returnCode;

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
                return returnCode;

            if ((returnCode = get_tree(arrBitLengthsForCodes, 64)) != 0)
                return returnCode;

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
                return returnCode;
        }
        else
        /* No literal tree--minimum match length is 2 */
        {
            if ((returnCode = get_tree(arrBitLengthsForCodes, 64)) != 0)
                return returnCode;

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
                return returnCode;
        }

        if ((returnCode = get_tree(arrBitLengthsForCodes, 64)) != 0)
            return (int)returnCode;

        if ((generalPurposeBitFlag & HeaderFlags.Bit1) != 0) /* true if 8K */
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
        else /* else 4K */
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

    private void NeedBits(int numberOfBits)
    {
        while (bitBufferCount < (numberOfBits))
        {
            bitBuffer |= (uint)inStream.ReadByte() << bitBufferCount;
            bitBufferCount += 8;
        }
    }

    private void DumpBits(int numberOfBits)
    {
        bitBuffer >>= numberOfBits;
        bitBufferCount -= numberOfBits;
    }

    int DecodeHuft(huftNode[] htab, int bits, uint mask, out huftNode huftPointer, out int e)
    {
        NeedBits(bits);

        int tabOffset = (int)(~bitBuffer & mask);
        huftPointer = htab[tabOffset];

        while (true)
        {
            DumpBits(huftPointer.NumberOfBitsUsed);
            e = huftPointer.NumberOfExtraBits;
            if (e <= 32)
                break;
            if (e == INVALID_CODE)
                return 1;

            e &= 31;
            NeedBits(e);

            tabOffset = (int)(~bitBuffer & mask_bits[e]);
            huftPointer = huftPointer.ChildNodes[tabOffset];
        }

        return 0;
    }

    private void explode_var_init()
    {
        /* explode the coded data */
        bitBuffer = 0;
        bitBufferCount = 0;
        maskForLiteralCodeTable = mask_bits[bitsForLiteralCodeTable]; //only used in explode_lit
        maskForLengthCodeTable = mask_bits[bitsForLengthCodeTable];
        maskForDistanceCodeTable = mask_bits[bitsForDistanceCodeTable];
        maskForDistanceLowBits = mask_bits[numOfUncodedLowerDistanceBits];
        outBytesCount = 0;

        windowIndex = 0; /* initialize bit buffer, window */
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int countIndex = 0;
        while (countIndex < count && outBytesCount < unCompressedSize) /* do until unCompressedSize bytes uncompressed */
        {
            if (length == 0)
            {
                NeedBits(1);
                bool literal = (bitBuffer & 1) == 1;
                DumpBits(1);

                huftNode huftPointer;
                if (literal) /* then literal--decode it */
                {
                    byte nextByte;
                    if (hufLiteralCodeTable != null)
                    {
                        /* get coded literal */
                        if (
                            DecodeHuft(
                                hufLiteralCodeTable,
                                bitsForLiteralCodeTable,
                                maskForLiteralCodeTable,
                                out huftPointer,
                                out _
                            ) != 0
                        )
                            throw new Exception("Error decoding literal value");

                        nextByte = (byte)huftPointer.Value;
                    }
                    else
                    {
                        NeedBits(8);
                        nextByte = (byte)bitBuffer;
                        DumpBits(8);
                    }

                    buffer[offset + (countIndex++)] = nextByte;
                    windowsBuffer[windowIndex++] = nextByte;
                    outBytesCount++;

                    if (windowIndex == WSIZE)
                        windowIndex = 0;

                    continue;
                }

                NeedBits(numOfUncodedLowerDistanceBits); /* get distance low bits */
                distance = (int)(bitBuffer & maskForDistanceLowBits);
                DumpBits(numOfUncodedLowerDistanceBits);

                /* get coded distance high bits */
                if (
                    DecodeHuft(
                        hufDistanceCodeTable,
                        bitsForDistanceCodeTable,
                        maskForDistanceCodeTable,
                        out huftPointer,
                        out _
                    ) != 0
                )
                    throw new Exception("Error decoding distance high bits");

                distance = windowIndex - (distance + huftPointer.Value); /* construct offset */

                /* get coded length */
                if (
                    DecodeHuft(
                        hufLengthCodeTable,
                        bitsForLengthCodeTable,
                        maskForLengthCodeTable,
                        out huftPointer,
                        out int extraBitLength
                    ) != 0
                )
                    throw new Exception("Error decoding coded length");

                length = huftPointer.Value;

                if (extraBitLength != 0) /* get length extra bits */
                {
                    NeedBits(8);
                    length += (int)(bitBuffer & 0xff);
                    DumpBits(8);
                }

                if (length > (unCompressedSize - outBytesCount))
                    length = (int)(unCompressedSize - outBytesCount);

                distance &= WSIZE - 1;
            }

            while (length != 0 && countIndex < count)
            {
                byte nextByte = windowsBuffer[distance++];
                buffer[offset + (countIndex++)] = nextByte;
                windowsBuffer[windowIndex++] = nextByte;
                outBytesCount++;

                if (distance == WSIZE)
                    distance = 0;

                if (windowIndex == WSIZE)
                    windowIndex = 0;

                length--;
            }
        }

        return countIndex;
    }
}
