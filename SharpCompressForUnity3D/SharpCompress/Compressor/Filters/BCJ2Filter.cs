namespace SharpCompress.Compressor.Filters
{
    using System;
    using System.IO;

    internal class BCJ2Filter : Stream
    {
        private readonly Stream baseStream;
        private uint code;
        private byte[] control;
        private int controlPos;
        private byte[] data1;
        private int data1Pos;
        private byte[] data2;
        private int data2Pos;
        private bool endReached;
        private readonly byte[] input;
        private int inputCount;
        private int inputOffset;
        private bool isDisposed;
        private const int kBitModelTotal = 0x800;
        private const int kNumBitModelTotalBits = 11;
        private const int kNumMoveBits = 5;
        private const int kNumTopBits = 0x18;
        private const int kTopValue = 0x1000000;
        private byte[] output;
        private int outputCount;
        private int outputOffset;
        private ushort[] p;
        private long position;
        private byte prevByte;
        private uint range;

        public BCJ2Filter(byte[] control, byte[] data1, byte[] data2, Stream baseStream)
        {
            int num;
            this.input = new byte[0x1000];
            this.inputOffset = 0;
            this.inputCount = 0;
            this.endReached = false;
            this.position = 0L;
            this.output = new byte[4];
            this.outputOffset = 0;
            this.outputCount = 0;
            this.controlPos = 0;
            this.data1Pos = 0;
            this.data2Pos = 0;
            this.p = new ushort[0x102];
            this.prevByte = 0;
            this.control = control;
            this.data1 = data1;
            this.data2 = data2;
            this.baseStream = baseStream;
            for (num = 0; num < this.p.Length; num++)
            {
                this.p[num] = 0x400;
            }
            this.code = 0;
            this.range = uint.MaxValue;
            for (num = 0; num < 5; num++)
            {
                this.code = (this.code << 8) | control[this.controlPos++];
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                this.baseStream.Dispose();
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
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
            int num = 0;
            byte num2 = 0;
            while (!this.endReached && (num < count))
            {
                while (this.outputOffset < this.outputCount)
                {
                    num2 = this.output[this.outputOffset++];
                    buffer[offset++] = num2;
                    num++;
                    this.position += 1L;
                    this.prevByte = num2;
                    if (num == count)
                    {
                        return num;
                    }
                }
                if (this.inputOffset == this.inputCount)
                {
                    this.inputOffset = 0;
                    this.inputCount = this.baseStream.Read(this.input, 0, this.input.Length);
                    if (this.inputCount == 0)
                    {
                        this.endReached = true;
                        return num;
                    }
                }
                num2 = this.input[this.inputOffset++];
                buffer[offset++] = num2;
                num++;
                this.position += 1L;
                if (!IsJ(this.prevByte, num2))
                {
                    this.prevByte = num2;
                }
                else
                {
                    int prevByte;
                    if (num2 == 0xe8)
                    {
                        prevByte = this.prevByte;
                    }
                    else if (num2 == 0xe9)
                    {
                        prevByte = 0x100;
                    }
                    else
                    {
                        prevByte = 0x101;
                    }
                    uint num4 = (this.range >> 11) * this.p[prevByte];
                    if (this.code < num4)
                    {
                        this.range = num4;
                        this.p[prevByte] = (ushort) (this.p[prevByte] + ((ushort) ((0x800 - this.p[prevByte]) >> 5)));
                        if (this.range < 0x1000000)
                        {
                            this.range = this.range << 8;
                            this.code = (this.code << 8) | this.control[this.controlPos++];
                        }
                        this.prevByte = num2;
                    }
                    else
                    {
                        uint num5;
                        this.range -= num4;
                        this.code -= num4;
                        this.p[prevByte] = (ushort) (this.p[prevByte] - ((ushort) (this.p[prevByte] >> 5)));
                        if (this.range < 0x1000000)
                        {
                            this.range = this.range << 8;
                            this.code = (this.code << 8) | this.control[this.controlPos++];
                        }
                        if (num2 == 0xe8)
                        {
                            num5 = (uint) ((((this.data1[this.data1Pos++] << 0x18) | (this.data1[this.data1Pos++] << 0x10)) | (this.data1[this.data1Pos++] << 8)) | this.data1[this.data1Pos++]);
                        }
                        else
                        {
                            num5 = (uint) ((((this.data2[this.data2Pos++] << 0x18) | (this.data2[this.data2Pos++] << 0x10)) | (this.data2[this.data2Pos++] << 8)) | this.data2[this.data2Pos++]);
                        }
                        num5 -= (uint) (this.position + 4L);
                        this.output[0] = (byte) num5;
                        this.output[1] = (byte) (num5 >> 8);
                        this.output[2] = (byte) (num5 >> 0x10);
                        this.output[3] = (byte) (num5 >> 0x18);
                        this.outputOffset = 0;
                        this.outputCount = 4;
                    }
                }
            }
            return num;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return ((this.baseStream.Length + this.data1.Length) + this.data2.Length);
            }
        }

        public override long Position
        {
            get
            {
                return this.position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}

