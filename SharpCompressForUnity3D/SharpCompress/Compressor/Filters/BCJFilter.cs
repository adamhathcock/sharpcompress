namespace SharpCompress.Compressor.Filters
{
    using System;
    using System.IO;

    internal class BCJFilter : SharpCompress.Compressor.Filters.Filter
    {
        private static readonly bool[] MASK_TO_ALLOWED_STATUS = new bool[] { true, true, true, false, true, false, false, false };
        private static readonly int[] MASK_TO_BIT_NUMBER = new int[] { 0, 1, 2, 2, 3, 3, 3, 3 };
        private int pos;
        private int prevMask;

        public BCJFilter(bool isEncoder, Stream baseStream) : base(isEncoder, baseStream, 5)
        {
            this.prevMask = 0;
            this.pos = 5;
        }

        private static bool test86MSByte(byte b)
        {
            return ((b == 0) || (b == 0xff));
        }

        protected override int Transform(byte[] buffer, int offset, int count)
        {
            int num = offset - 1;
            int num2 = (offset + count) - 5;
            int index = offset;
            while (index <= num2)
            {
                int num5;
                bool flag;
                if ((buffer[index] & 0xfe) != 0xe8)
                {
                    goto Label_01C2;
                }
                num = index - num;
                if ((num & -4) != 0)
                {
                    this.prevMask = 0;
                }
                else
                {
                    this.prevMask = (this.prevMask << (num - 1)) & 7;
                    if ((this.prevMask != 0) && !(MASK_TO_ALLOWED_STATUS[this.prevMask] && !test86MSByte(buffer[(index + 4) - MASK_TO_BIT_NUMBER[this.prevMask]])))
                    {
                        num = index;
                        this.prevMask = (this.prevMask << 1) | 1;
                        goto Label_01C2;
                    }
                }
                num = index;
                if (!test86MSByte(buffer[index + 4]))
                {
                    goto Label_01AF;
                }
                int num4 = ((buffer[index + 1] | (buffer[index + 2] << 8)) | (buffer[index + 3] << 0x10)) | (buffer[index + 4] << 0x18);
                goto Label_0173;
            Label_00F7:
                if (base.isEncoder)
                {
                    num5 = num4 + ((this.pos + index) - offset);
                }
                else
                {
                    num5 = num4 - ((this.pos + index) - offset);
                }
                if (this.prevMask == 0)
                {
                    goto Label_017B;
                }
                int num6 = MASK_TO_BIT_NUMBER[this.prevMask] * 8;
                if (!test86MSByte((byte) (num5 >> (0x18 - num6))))
                {
                    goto Label_017B;
                }
                num4 = num5 ^ ((((int) 1) << (0x20 - num6)) - 1);
            Label_0173:
                flag = true;
                goto Label_00F7;
            Label_017B:
                buffer[index + 1] = (byte) num5;
                buffer[index + 2] = (byte) (num5 >> 8);
                buffer[index + 3] = (byte) (num5 >> 0x10);
                buffer[index + 4] = (byte) ~(((num5 >> 0x18) & 1) - 1);
                index += 4;
                goto Label_01C2;
            Label_01AF:
                this.prevMask = (this.prevMask << 1) | 1;
            Label_01C2:
                index++;
            }
            num = index - num;
            this.prevMask = ((num & -4) != 0) ? 0 : (this.prevMask << (num - 1));
            index -= offset;
            this.pos += index;
            return index;
        }
    }
}

