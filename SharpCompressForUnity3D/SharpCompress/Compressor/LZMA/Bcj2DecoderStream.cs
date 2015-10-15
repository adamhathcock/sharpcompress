namespace SharpCompress.Compressor.LZMA
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal class Bcj2DecoderStream : DecoderStream2
    {
        private bool isDisposed;
        private const int kNumTopBits = 0x18;
        private const uint kTopValue = 0x1000000;
        private Stream mCallStream;
        private bool mFinished;
        private IEnumerator<byte> mIter;
        private Stream mJumpStream;
        private long mLimit;
        private Stream mMainStream;
        private RangeDecoder mRangeDecoder;
        private StatusDecoder[] mStatusDecoder;
        private long mWritten;

        public Bcj2DecoderStream(Stream[] streams, byte[] info, long limit)
        {
            if ((info != null) && (info.Length > 0))
            {
                throw new NotSupportedException();
            }
            if (streams.Length != 4)
            {
                throw new NotSupportedException();
            }
            this.mLimit = limit;
            this.mMainStream = streams[0];
            this.mCallStream = streams[1];
            this.mJumpStream = streams[2];
            this.mRangeDecoder = new RangeDecoder(streams[3]);
            this.mStatusDecoder = new StatusDecoder[0x102];
            for (int i = 0; i < this.mStatusDecoder.Length; i++)
            {
                this.mStatusDecoder[i] = new StatusDecoder();
            }
            this.mIter = this.Run().GetEnumerator();
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                this.mMainStream.Dispose();
                this.mCallStream.Dispose();
                this.mJumpStream.Dispose();
            }
        }

        private static int GetIndex(byte b0, byte b1)
        {
            if (b1 == 0xe8)
            {
                return b0;
            }
            if (b1 == 0xe9)
            {
                return 0x100;
            }
            return 0x101;
        }

        private static bool IsJ(byte b0, byte b1)
        {
            return (((b1 & 0xfe) == 0xe8) || IsJcc(b0, b1));
        }

        private static bool IsJcc(byte b0, byte b1)
        {
            return ((b0 == 15) && ((b1 & 240) == 0x80));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if ((count == 0) || this.mFinished)
            {
                return 0;
            }
            for (int i = 0; i < count; i++)
            {
                if (!this.mIter.MoveNext())
                {
                    this.mFinished = true;
                    return i;
                }
                buffer[offset + i] = this.mIter.Current;
            }
            return count;
        }

        public IEnumerable<byte> Run()
        {
            byte iteratorVariable1 = 0;
            uint iteratorVariable2 = 0;
            while (true)
            {
                uint iteratorVariable4;
                byte iteratorVariable3 = 0;
                for (iteratorVariable4 = 0; iteratorVariable4 < 0x40000; iteratorVariable4++)
                {
                    int iteratorVariable5 = this.mMainStream.ReadByte();
                    if (iteratorVariable5 < 0)
                    {
                        break;
                    }
                    iteratorVariable3 = (byte) iteratorVariable5;
                    this.mWritten += 1L;
                    yield return iteratorVariable3;
                    if (IsJ(iteratorVariable1, iteratorVariable3))
                    {
                        break;
                    }
                    iteratorVariable1 = iteratorVariable3;
                }
                iteratorVariable2 += iteratorVariable4;
                if (iteratorVariable4 != 0x40000)
                {
                    if (this.mStatusDecoder[GetIndex(iteratorVariable1, iteratorVariable3)].Decode(this.mRangeDecoder) == 1)
                    {
                        Stream iteratorVariable6 = (iteratorVariable3 == 0xe8) ? this.mCallStream : this.mJumpStream;
                        uint iteratorVariable7 = 0;
                        for (iteratorVariable4 = 0; iteratorVariable4 < 4; iteratorVariable4++)
                        {
                            int num = iteratorVariable6.ReadByte();
                            if (num < 0)
                            {
                                throw new EndOfStreamException();
                            }
                            iteratorVariable7 = iteratorVariable7 << 8;
                            iteratorVariable7 |= (uint) num;
                        }
                        uint iteratorVariable8 = iteratorVariable7 - ((uint) (this.mWritten + 4L));
                        this.mWritten += 1L;
                        yield return (byte) iteratorVariable8;
                        this.mWritten += 1L;
                        yield return (byte) (iteratorVariable8 >> 8);
                        this.mWritten += 1L;
                        yield return (byte) (iteratorVariable8 >> 0x10);
                        this.mWritten += 1L;
                        yield return (byte) (iteratorVariable8 >> 0x18);
                        iteratorVariable1 = (byte) (iteratorVariable8 >> 0x18);
                        iteratorVariable2 += 4;
                    }
                    else
                    {
                        iteratorVariable1 = iteratorVariable3;
                    }
                }
            }
        }


        private class RangeDecoder
        {
            internal uint Code;
            internal Stream mStream;
            internal uint Range;

            public RangeDecoder(Stream stream)
            {
                this.mStream = stream;
                this.Range = uint.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    this.Code = (this.Code << 8) | this.ReadByte();
                }
            }

            public void Dispose()
            {
                this.mStream.Dispose();
            }

            public byte ReadByte()
            {
                int num = this.mStream.ReadByte();
                if (num < 0)
                {
                    throw new EndOfStreamException();
                }
                return (byte) num;
            }
        }

        private class StatusDecoder
        {
            private const uint kBitModelTotal = 0x800;
            private const int kNumBitModelTotalBits = 11;
            private const int numMoveBits = 5;
            private uint Prob = 0x400;

            public uint Decode(Bcj2DecoderStream.RangeDecoder decoder)
            {
                uint num = (decoder.Range >> 11) * this.Prob;
                if (decoder.Code < num)
                {
                    decoder.Range = num;
                    this.Prob += (uint) ((0x800 - this.Prob) >> 5);
                    if (decoder.Range < 0x1000000)
                    {
                        decoder.Code = (decoder.Code << 8) | decoder.ReadByte();
                        decoder.Range = decoder.Range << 8;
                    }
                    return 0;
                }
                decoder.Range -= num;
                decoder.Code -= num;
                this.Prob -= this.Prob >> 5;
                if (decoder.Range < 0x1000000)
                {
                    decoder.Code = (decoder.Code << 8) | decoder.ReadByte();
                    decoder.Range = decoder.Range << 8;
                }
                return 1;
            }

            private void UpdateModel(uint symbol)
            {
                if (symbol == 0)
                {
                    this.Prob += (uint) ((0x800 - this.Prob) >> 5);
                }
                else
                {
                    this.Prob -= this.Prob >> 5;
                }
            }
        }
    }
}

