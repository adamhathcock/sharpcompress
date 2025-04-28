using System;
using System.IO;

namespace SharpCompress.Compressors.Reduce;

public class ReduceStream : Stream
{
    private readonly long unCompressedSize;
    private readonly long compressedSize;
    private readonly Stream inStream;

    private long inByteCount;
    private const int EOF = 1234;

    private readonly int factor;
    private readonly int distanceMask;
    private readonly int lengthMask;

    private long outBytesCount;

    private readonly byte[] windowsBuffer;
    private int windowIndex;
    private int length;
    private int distance;

    public ReduceStream(Stream inStr, long compsize, long unCompSize, int factor)
    {
        inStream = inStr;
        compressedSize = compsize;
        unCompressedSize = unCompSize;
        inByteCount = 0;
        outBytesCount = 0;

        this.factor = factor;
        distanceMask = (int)mask_bits[factor] << 8;
        lengthMask = 0xff >> factor;

        windowIndex = 0;
        length = 0;
        distance = 0;

        windowsBuffer = new byte[WSIZE];

        outByte = 0;

        LoadBitLengthTable();
        LoadNextByteTable();
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

    private const int RunLengthCode = 144;
    private const int WSIZE = 0x4000;

    private readonly uint[] mask_bits = new uint[]
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

    private int bitBufferCount;
    private ulong bitBuffer;

    private int NEXTBYTE()
    {
        if (inByteCount == compressedSize)
            return EOF;
        inByteCount++;
        return inStream.ReadByte();
    }

    private void READBITS(int nbits, out byte zdest)
    {
        if (nbits > bitBufferCount)
        {
            int temp;
            while (bitBufferCount <= 8 * (int)(4 - 1) && (temp = NEXTBYTE()) != EOF)
            {
                bitBuffer |= (ulong)temp << bitBufferCount;
                bitBufferCount += 8;
            }
        }
        zdest = (byte)(bitBuffer & (ulong)mask_bits[nbits]);
        bitBuffer >>= nbits;
        bitBufferCount -= nbits;
    }

    private byte[] bitCountTable = [];

    private void LoadBitLengthTable()
    {
        byte[] bitPos = { 0, 2, 4, 8, 16, 32, 64, 128, 255 };
        bitCountTable = new byte[256];

        for (byte i = 1; i <= 8; i++)
        {
            int vMin = bitPos[i - 1] + 1;
            int vMax = bitPos[i];
            for (int j = vMin; j <= vMax; j++)
            {
                bitCountTable[j] = i;
            }
        }
    }

    private byte[][] nextByteTable = [];

    private void LoadNextByteTable()
    {
        nextByteTable = new byte[256][];
        for (int x = 255; x >= 0; x--)
        {
            READBITS(6, out byte Slen);
            nextByteTable[x] = new byte[Slen];
            for (int i = 0; i < Slen; i++)
            {
                READBITS(8, out nextByteTable[x][i]);
            }
        }
    }

    private byte outByte;

    private byte GetNextByte()
    {
        if (nextByteTable[outByte].Length == 0)
        {
            READBITS(8, out outByte);
            return outByte;
        }
        READBITS(1, out byte nextBit);
        if (nextBit == 1)
        {
            READBITS(8, out outByte);
            return outByte;
        }
        READBITS(bitCountTable[nextByteTable[outByte].Length], out byte nextByteIndex);
        outByte = nextByteTable[outByte][nextByteIndex];
        return outByte;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int countIndex = 0;
        while (countIndex < count && outBytesCount < unCompressedSize)
        {
            if (length == 0)
            {
                byte nextByte = GetNextByte();
                if (nextByte != RunLengthCode)
                {
                    buffer[offset + (countIndex++)] = nextByte;
                    windowsBuffer[windowIndex++] = nextByte;
                    outBytesCount++;
                    if (windowIndex == WSIZE)
                        windowIndex = 0;

                    continue;
                }

                nextByte = GetNextByte();
                if (nextByte == 0)
                {
                    buffer[offset + (countIndex++)] = RunLengthCode;
                    windowsBuffer[windowIndex++] = RunLengthCode;
                    outBytesCount++;
                    if (windowIndex == WSIZE)
                        windowIndex = 0;

                    continue;
                }

                int lengthDistanceByte = nextByte;
                length = lengthDistanceByte & lengthMask;
                if (length == lengthMask)
                {
                    length += GetNextByte();
                }
                length += 3;

                int distanceHighByte = (lengthDistanceByte << factor) & distanceMask;
                distance = windowIndex - (distanceHighByte + GetNextByte() + 1);

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
