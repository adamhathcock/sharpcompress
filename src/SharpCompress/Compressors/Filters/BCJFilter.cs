using System.IO;

namespace SharpCompress.Compressors.Filters
{
    internal class BCJFilter : Filter
    {
        private static readonly bool[] MASK_TO_ALLOWED_STATUS = {true, true, true, false, true, false, false, false};

        private static readonly int[] MASK_TO_BIT_NUMBER = {0, 1, 2, 2, 3, 3, 3, 3};

        private int pos;
        private int prevMask;

        public BCJFilter(bool isEncoder, Stream baseStream)
            : base(isEncoder, baseStream, 5)
        {
            pos = 5;
        }

        private static bool test86MSByte(byte b)
        {
            return b == 0x00 || b == 0xFF;
        }

        protected override int Transform(byte[] buffer, int offset, int count)
        {
            int prevPos = offset - 1;
            int end = offset + count - 5;
            int i;

            for (i = offset; i <= end; ++i)
            {
                if ((buffer[i] & 0xFE) != 0xE8)
                {
                    continue;
                }

                prevPos = i - prevPos;
                if ((prevPos & ~3) != 0)
                {
                    // (unsigned)prevPos > 3
                    prevMask = 0;
                }
                else
                {
                    prevMask = (prevMask << (prevPos - 1)) & 7;
                    if (prevMask != 0)
                    {
                        if (!MASK_TO_ALLOWED_STATUS[prevMask] || test86MSByte(
                                                                              buffer[i + 4 - MASK_TO_BIT_NUMBER[prevMask]]))
                        {
                            prevPos = i;
                            prevMask = (prevMask << 1) | 1;
                            continue;
                        }
                    }
                }

                prevPos = i;

                if (test86MSByte(buffer[i + 4]))
                {
                    int src = buffer[i + 1]
                              | (buffer[i + 2] << 8)
                              | (buffer[i + 3] << 16)
                              | (buffer[i + 4] << 24);
                    int dest;
                    while (true)
                    {
                        if (isEncoder)
                        {
                            dest = src + (pos + i - offset);
                        }
                        else
                        {
                            dest = src - (pos + i - offset);
                        }

                        if (prevMask == 0)
                        {
                            break;
                        }

                        int index = MASK_TO_BIT_NUMBER[prevMask] * 8;
                        if (!test86MSByte((byte)(dest >> (24 - index))))
                        {
                            break;
                        }

                        src = dest ^ ((1 << (32 - index)) - 1);
                    }

                    buffer[i + 1] = (byte)dest;
                    buffer[i + 2] = (byte)(dest >> 8);
                    buffer[i + 3] = (byte)(dest >> 16);
                    buffer[i + 4] = (byte)(~(((dest >> 24) & 1) - 1));
                    i += 4;
                }
                else
                {
                    prevMask = (prevMask << 1) | 1;
                }
            }

            prevPos = i - prevPos;
            prevMask = ((prevPos & ~3) != 0) ? 0 : prevMask << (prevPos - 1);

            i -= offset;
            pos += i;
            return i;
        }
    }
}