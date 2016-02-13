using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Compressor.LZMA
{
    internal class Bcj2DecoderStream : DecoderStream2
    {
        private const int kNumTopBits = 24;
        private const uint kTopValue = (1 << kNumTopBits);

        private class RangeDecoder
        {
            internal Stream mStream;
            internal uint Range;
            internal uint Code;

            public RangeDecoder(Stream stream)
            {
                mStream = stream;
                Range = 0xFFFFFFFF;
                for (int i = 0; i < 5; i++)
                    Code = (Code << 8) | ReadByte();
            }

            public byte ReadByte()
            {
                int bt = mStream.ReadByte();
                if (bt < 0)
                    throw new EndOfStreamException();

                return (byte)bt;
            }

            public void Dispose()
            {
                mStream.Dispose();
            }
        }

        private class StatusDecoder
        {
            private const int numMoveBits = 5;

            private const int kNumBitModelTotalBits = 11;
            private const uint kBitModelTotal = 1u << kNumBitModelTotalBits;

            private uint Prob;

            public StatusDecoder()
            {
                Prob = kBitModelTotal / 2;
            }

            private void UpdateModel(uint symbol)
            {
                /*
                Prob -= (Prob + ((symbol - 1) & ((1 << numMoveBits) - 1))) >> numMoveBits;
                Prob += (1 - symbol) << (kNumBitModelTotalBits - numMoveBits);
                */
                if (symbol == 0)
                    Prob += (kBitModelTotal - Prob) >> numMoveBits;
                else
                    Prob -= (Prob) >> numMoveBits;
            }

            public uint Decode(RangeDecoder decoder)
            {
                uint newBound = (decoder.Range >> kNumBitModelTotalBits) * Prob;
                if (decoder.Code < newBound)
                {
                    decoder.Range = newBound;
                    Prob += (kBitModelTotal - Prob) >> numMoveBits;
                    if (decoder.Range < kTopValue)
                    {
                        decoder.Code = (decoder.Code << 8) | decoder.ReadByte();
                        decoder.Range <<= 8;
                    }
                    return 0;
                }
                else
                {
                    decoder.Range -= newBound;
                    decoder.Code -= newBound;
                    Prob -= Prob >> numMoveBits;
                    if (decoder.Range < kTopValue)
                    {
                        decoder.Code = (decoder.Code << 8) | decoder.ReadByte();
                        decoder.Range <<= 8;
                    }
                    return 1;
                }
            }
        }

        private Stream mMainStream;
        private Stream mCallStream;
        private Stream mJumpStream;
        private RangeDecoder mRangeDecoder;
        private StatusDecoder[] mStatusDecoder;
        private long mWritten;
        private IEnumerator<byte> mIter;
        private bool mFinished;
        private bool isDisposed;

        public Bcj2DecoderStream(Stream[] streams, byte[] info, long limit)
        {
            if (info != null && info.Length > 0)
                throw new NotSupportedException();

            if (streams.Length != 4)
                throw new NotSupportedException();

            mMainStream = streams[0];
            mCallStream = streams[1];
            mJumpStream = streams[2];
            mRangeDecoder = new RangeDecoder(streams[3]);

            mStatusDecoder = new StatusDecoder[256 + 2];
            for (int i = 0; i < mStatusDecoder.Length; i++)
                mStatusDecoder[i] = new StatusDecoder();

            mIter = Run().GetEnumerator();
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            base.Dispose(disposing);
            mMainStream.Dispose();
            mCallStream.Dispose();
            mJumpStream.Dispose();
        }

        private static bool IsJcc(byte b0, byte b1)
        {
            return b0 == 0x0F
                   && (b1 & 0xF0) == 0x80;
        }

        private static bool IsJ(byte b0, byte b1)
        {
            return (b1 & 0xFE) == 0xE8
                   || IsJcc(b0, b1);
        }

        private static int GetIndex(byte b0, byte b1)
        {
            if (b1 == 0xE8)
                return b0;
            else if (b1 == 0xE9)
                return 256;
            else
                return 257;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0 || mFinished)
                return 0;

            for (int i = 0; i < count; i++)
            {
                if (!mIter.MoveNext())
                {
                    mFinished = true;
                    return i;
                }

                buffer[offset + i] = mIter.Current;
            }

            return count;
        }

        public IEnumerable<byte> Run()
        {
            const uint kBurstSize = (1u << 18);

            byte prevByte = 0;
            uint processedBytes = 0;
            for (; ; )
            {
                byte b = 0;
                uint i;
                for (i = 0; i < kBurstSize; i++)
                {
                    int tmp = mMainStream.ReadByte();
                    if (tmp < 0)
                        yield break;

                    b = (byte)tmp;
                    mWritten++;
                    yield return b;
                    if (IsJ(prevByte, b))
                        break;

                    prevByte = b;
                }

                processedBytes += i;
                if (i == kBurstSize)
                    continue;

                if (mStatusDecoder[GetIndex(prevByte, b)].Decode(mRangeDecoder) == 1)
                {
                    Stream s = (b == 0xE8) ? mCallStream : mJumpStream;

                    uint src = 0;
                    for (i = 0; i < 4; i++)
                    {
                        int b0 = s.ReadByte();
                        if (b0 < 0)
                            throw new EndOfStreamException();

                        src <<= 8;
                        src |= (uint)b0;
                    }

                    uint dest = src - (uint)(mWritten + 4);
                    mWritten++;
                    yield return (byte)dest;
                    mWritten++;
                    yield return (byte)(dest >> 8);
                    mWritten++;
                    yield return (byte)(dest >> 16);
                    mWritten++;
                    yield return (byte)(dest >> 24);
                    prevByte = (byte)(dest >> 24);
                    processedBytes += 4;
                }
                else
                {
                    prevByte = b;
                }
            }
        }
    }
}