namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal sealed class InfTree
    {
        internal const int BMAX = 15;
        internal int[] c;
        internal static readonly int[] cpdext = new int[] { 
            0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 
            7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13
         };
        internal static readonly int[] cpdist = new int[] { 
            1, 2, 3, 4, 5, 7, 9, 13, 0x11, 0x19, 0x21, 0x31, 0x41, 0x61, 0x81, 0xc1, 
            0x101, 0x181, 0x201, 0x301, 0x401, 0x601, 0x801, 0xc01, 0x1001, 0x1801, 0x2001, 0x3001, 0x4001, 0x6001
         };
        internal static readonly int[] cplens = new int[] { 
            3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 0x11, 0x13, 0x17, 0x1b, 0x1f, 
            0x23, 0x2b, 0x33, 0x3b, 0x43, 0x53, 0x63, 0x73, 0x83, 0xa3, 0xc3, 0xe3, 0x102, 0, 0
         };
        internal static readonly int[] cplext = new int[] { 
            0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 
            3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0, 0x70, 0x70
         };
        internal const int fixed_bd = 5;
        internal const int fixed_bl = 9;
        internal static readonly int[] fixed_td = new int[] { 
            80, 5, 1, 0x57, 5, 0x101, 0x53, 5, 0x11, 0x5b, 5, 0x1001, 0x51, 5, 5, 0x59, 
            5, 0x401, 0x55, 5, 0x41, 0x5d, 5, 0x4001, 80, 5, 3, 0x58, 5, 0x201, 0x54, 5, 
            0x21, 0x5c, 5, 0x2001, 0x52, 5, 9, 90, 5, 0x801, 0x56, 5, 0x81, 0xc0, 5, 0x6001, 
            80, 5, 2, 0x57, 5, 0x181, 0x53, 5, 0x19, 0x5b, 5, 0x1801, 0x51, 5, 7, 0x59, 
            5, 0x601, 0x55, 5, 0x61, 0x5d, 5, 0x6001, 80, 5, 4, 0x58, 5, 0x301, 0x54, 5, 
            0x31, 0x5c, 5, 0x3001, 0x52, 5, 13, 90, 5, 0xc01, 0x56, 5, 0xc1, 0xc0, 5, 0x6001
         };
        internal static readonly int[] fixed_tl = new int[] { 
            0x60, 7, 0x100, 0, 8, 80, 0, 8, 0x10, 0x54, 8, 0x73, 0x52, 7, 0x1f, 0, 
            8, 0x70, 0, 8, 0x30, 0, 9, 0xc0, 80, 7, 10, 0, 8, 0x60, 0, 8, 
            0x20, 0, 9, 160, 0, 8, 0, 0, 8, 0x80, 0, 8, 0x40, 0, 9, 0xe0, 
            80, 7, 6, 0, 8, 0x58, 0, 8, 0x18, 0, 9, 0x90, 0x53, 7, 0x3b, 0, 
            8, 120, 0, 8, 0x38, 0, 9, 0xd0, 0x51, 7, 0x11, 0, 8, 0x68, 0, 8, 
            40, 0, 9, 0xb0, 0, 8, 8, 0, 8, 0x88, 0, 8, 0x48, 0, 9, 240, 
            80, 7, 4, 0, 8, 0x54, 0, 8, 20, 0x55, 8, 0xe3, 0x53, 7, 0x2b, 0, 
            8, 0x74, 0, 8, 0x34, 0, 9, 200, 0x51, 7, 13, 0, 8, 100, 0, 8, 
            0x24, 0, 9, 0xa8, 0, 8, 4, 0, 8, 0x84, 0, 8, 0x44, 0, 9, 0xe8, 
            80, 7, 8, 0, 8, 0x5c, 0, 8, 0x1c, 0, 9, 0x98, 0x54, 7, 0x53, 0, 
            8, 0x7c, 0, 8, 60, 0, 9, 0xd8, 0x52, 7, 0x17, 0, 8, 0x6c, 0, 8, 
            0x2c, 0, 9, 0xb8, 0, 8, 12, 0, 8, 140, 0, 8, 0x4c, 0, 9, 0xf8, 
            80, 7, 3, 0, 8, 0x52, 0, 8, 0x12, 0x55, 8, 0xa3, 0x53, 7, 0x23, 0, 
            8, 0x72, 0, 8, 50, 0, 9, 0xc4, 0x51, 7, 11, 0, 8, 0x62, 0, 8, 
            0x22, 0, 9, 0xa4, 0, 8, 2, 0, 8, 130, 0, 8, 0x42, 0, 9, 0xe4, 
            80, 7, 7, 0, 8, 90, 0, 8, 0x1a, 0, 9, 0x94, 0x54, 7, 0x43, 0, 
            8, 0x7a, 0, 8, 0x3a, 0, 9, 0xd4, 0x52, 7, 0x13, 0, 8, 0x6a, 0, 8, 
            0x2a, 0, 9, 180, 0, 8, 10, 0, 8, 0x8a, 0, 8, 0x4a, 0, 9, 0xf4, 
            80, 7, 5, 0, 8, 0x56, 0, 8, 0x16, 0xc0, 8, 0, 0x53, 7, 0x33, 0, 
            8, 0x76, 0, 8, 0x36, 0, 9, 0xcc, 0x51, 7, 15, 0, 8, 0x66, 0, 8, 
            0x26, 0, 9, 0xac, 0, 8, 6, 0, 8, 0x86, 0, 8, 70, 0, 9, 0xec, 
            80, 7, 9, 0, 8, 0x5e, 0, 8, 30, 0, 9, 0x9c, 0x54, 7, 0x63, 0, 
            8, 0x7e, 0, 8, 0x3e, 0, 9, 220, 0x52, 7, 0x1b, 0, 8, 110, 0, 8, 
            0x2e, 0, 9, 0xbc, 0, 8, 14, 0, 8, 0x8e, 0, 8, 0x4e, 0, 9, 0xfc, 
            0x60, 7, 0x100, 0, 8, 0x51, 0, 8, 0x11, 0x55, 8, 0x83, 0x52, 7, 0x1f, 0, 
            8, 0x71, 0, 8, 0x31, 0, 9, 0xc2, 80, 7, 10, 0, 8, 0x61, 0, 8, 
            0x21, 0, 9, 0xa2, 0, 8, 1, 0, 8, 0x81, 0, 8, 0x41, 0, 9, 0xe2, 
            80, 7, 6, 0, 8, 0x59, 0, 8, 0x19, 0, 9, 0x92, 0x53, 7, 0x3b, 0, 
            8, 0x79, 0, 8, 0x39, 0, 9, 210, 0x51, 7, 0x11, 0, 8, 0x69, 0, 8, 
            0x29, 0, 9, 0xb2, 0, 8, 9, 0, 8, 0x89, 0, 8, 0x49, 0, 9, 0xf2, 
            80, 7, 4, 0, 8, 0x55, 0, 8, 0x15, 80, 8, 0x102, 0x53, 7, 0x2b, 0, 
            8, 0x75, 0, 8, 0x35, 0, 9, 0xca, 0x51, 7, 13, 0, 8, 0x65, 0, 8, 
            0x25, 0, 9, 170, 0, 8, 5, 0, 8, 0x85, 0, 8, 0x45, 0, 9, 0xea, 
            80, 7, 8, 0, 8, 0x5d, 0, 8, 0x1d, 0, 9, 0x9a, 0x54, 7, 0x53, 0, 
            8, 0x7d, 0, 8, 0x3d, 0, 9, 0xda, 0x52, 7, 0x17, 0, 8, 0x6d, 0, 8, 
            0x2d, 0, 9, 0xba, 0, 8, 13, 0, 8, 0x8d, 0, 8, 0x4d, 0, 9, 250, 
            80, 7, 3, 0, 8, 0x53, 0, 8, 0x13, 0x55, 8, 0xc3, 0x53, 7, 0x23, 0, 
            8, 0x73, 0, 8, 0x33, 0, 9, 0xc6, 0x51, 7, 11, 0, 8, 0x63, 0, 8, 
            0x23, 0, 9, 0xa6, 0, 8, 3, 0, 8, 0x83, 0, 8, 0x43, 0, 9, 230, 
            80, 7, 7, 0, 8, 0x5b, 0, 8, 0x1b, 0, 9, 150, 0x54, 7, 0x43, 0, 
            8, 0x7b, 0, 8, 0x3b, 0, 9, 0xd6, 0x52, 7, 0x13, 0, 8, 0x6b, 0, 8, 
            0x2b, 0, 9, 0xb6, 0, 8, 11, 0, 8, 0x8b, 0, 8, 0x4b, 0, 9, 0xf6, 
            80, 7, 5, 0, 8, 0x57, 0, 8, 0x17, 0xc0, 8, 0, 0x53, 7, 0x33, 0, 
            8, 0x77, 0, 8, 0x37, 0, 9, 0xce, 0x51, 7, 15, 0, 8, 0x67, 0, 8, 
            0x27, 0, 9, 0xae, 0, 8, 7, 0, 8, 0x87, 0, 8, 0x47, 0, 9, 0xee, 
            80, 7, 9, 0, 8, 0x5f, 0, 8, 0x1f, 0, 9, 0x9e, 0x54, 7, 0x63, 0, 
            8, 0x7f, 0, 8, 0x3f, 0, 9, 0xde, 0x52, 7, 0x1b, 0, 8, 0x6f, 0, 8, 
            0x2f, 0, 9, 190, 0, 8, 15, 0, 8, 0x8f, 0, 8, 0x4f, 0, 9, 0xfe, 
            0x60, 7, 0x100, 0, 8, 80, 0, 8, 0x10, 0x54, 8, 0x73, 0x52, 7, 0x1f, 0, 
            8, 0x70, 0, 8, 0x30, 0, 9, 0xc1, 80, 7, 10, 0, 8, 0x60, 0, 8, 
            0x20, 0, 9, 0xa1, 0, 8, 0, 0, 8, 0x80, 0, 8, 0x40, 0, 9, 0xe1, 
            80, 7, 6, 0, 8, 0x58, 0, 8, 0x18, 0, 9, 0x91, 0x53, 7, 0x3b, 0, 
            8, 120, 0, 8, 0x38, 0, 9, 0xd1, 0x51, 7, 0x11, 0, 8, 0x68, 0, 8, 
            40, 0, 9, 0xb1, 0, 8, 8, 0, 8, 0x88, 0, 8, 0x48, 0, 9, 0xf1, 
            80, 7, 4, 0, 8, 0x54, 0, 8, 20, 0x55, 8, 0xe3, 0x53, 7, 0x2b, 0, 
            8, 0x74, 0, 8, 0x34, 0, 9, 0xc9, 0x51, 7, 13, 0, 8, 100, 0, 8, 
            0x24, 0, 9, 0xa9, 0, 8, 4, 0, 8, 0x84, 0, 8, 0x44, 0, 9, 0xe9, 
            80, 7, 8, 0, 8, 0x5c, 0, 8, 0x1c, 0, 9, 0x99, 0x54, 7, 0x53, 0, 
            8, 0x7c, 0, 8, 60, 0, 9, 0xd9, 0x52, 7, 0x17, 0, 8, 0x6c, 0, 8, 
            0x2c, 0, 9, 0xb9, 0, 8, 12, 0, 8, 140, 0, 8, 0x4c, 0, 9, 0xf9, 
            80, 7, 3, 0, 8, 0x52, 0, 8, 0x12, 0x55, 8, 0xa3, 0x53, 7, 0x23, 0, 
            8, 0x72, 0, 8, 50, 0, 9, 0xc5, 0x51, 7, 11, 0, 8, 0x62, 0, 8, 
            0x22, 0, 9, 0xa5, 0, 8, 2, 0, 8, 130, 0, 8, 0x42, 0, 9, 0xe5, 
            80, 7, 7, 0, 8, 90, 0, 8, 0x1a, 0, 9, 0x95, 0x54, 7, 0x43, 0, 
            8, 0x7a, 0, 8, 0x3a, 0, 9, 0xd5, 0x52, 7, 0x13, 0, 8, 0x6a, 0, 8, 
            0x2a, 0, 9, 0xb5, 0, 8, 10, 0, 8, 0x8a, 0, 8, 0x4a, 0, 9, 0xf5, 
            80, 7, 5, 0, 8, 0x56, 0, 8, 0x16, 0xc0, 8, 0, 0x53, 7, 0x33, 0, 
            8, 0x76, 0, 8, 0x36, 0, 9, 0xcd, 0x51, 7, 15, 0, 8, 0x66, 0, 8, 
            0x26, 0, 9, 0xad, 0, 8, 6, 0, 8, 0x86, 0, 8, 70, 0, 9, 0xed, 
            80, 7, 9, 0, 8, 0x5e, 0, 8, 30, 0, 9, 0x9d, 0x54, 7, 0x63, 0, 
            8, 0x7e, 0, 8, 0x3e, 0, 9, 0xdd, 0x52, 7, 0x1b, 0, 8, 110, 0, 8, 
            0x2e, 0, 9, 0xbd, 0, 8, 14, 0, 8, 0x8e, 0, 8, 0x4e, 0, 9, 0xfd, 
            0x60, 7, 0x100, 0, 8, 0x51, 0, 8, 0x11, 0x55, 8, 0x83, 0x52, 7, 0x1f, 0, 
            8, 0x71, 0, 8, 0x31, 0, 9, 0xc3, 80, 7, 10, 0, 8, 0x61, 0, 8, 
            0x21, 0, 9, 0xa3, 0, 8, 1, 0, 8, 0x81, 0, 8, 0x41, 0, 9, 0xe3, 
            80, 7, 6, 0, 8, 0x59, 0, 8, 0x19, 0, 9, 0x93, 0x53, 7, 0x3b, 0, 
            8, 0x79, 0, 8, 0x39, 0, 9, 0xd3, 0x51, 7, 0x11, 0, 8, 0x69, 0, 8, 
            0x29, 0, 9, 0xb3, 0, 8, 9, 0, 8, 0x89, 0, 8, 0x49, 0, 9, 0xf3, 
            80, 7, 4, 0, 8, 0x55, 0, 8, 0x15, 80, 8, 0x102, 0x53, 7, 0x2b, 0, 
            8, 0x75, 0, 8, 0x35, 0, 9, 0xcb, 0x51, 7, 13, 0, 8, 0x65, 0, 8, 
            0x25, 0, 9, 0xab, 0, 8, 5, 0, 8, 0x85, 0, 8, 0x45, 0, 9, 0xeb, 
            80, 7, 8, 0, 8, 0x5d, 0, 8, 0x1d, 0, 9, 0x9b, 0x54, 7, 0x53, 0, 
            8, 0x7d, 0, 8, 0x3d, 0, 9, 0xdb, 0x52, 7, 0x17, 0, 8, 0x6d, 0, 8, 
            0x2d, 0, 9, 0xbb, 0, 8, 13, 0, 8, 0x8d, 0, 8, 0x4d, 0, 9, 0xfb, 
            80, 7, 3, 0, 8, 0x53, 0, 8, 0x13, 0x55, 8, 0xc3, 0x53, 7, 0x23, 0, 
            8, 0x73, 0, 8, 0x33, 0, 9, 0xc7, 0x51, 7, 11, 0, 8, 0x63, 0, 8, 
            0x23, 0, 9, 0xa7, 0, 8, 3, 0, 8, 0x83, 0, 8, 0x43, 0, 9, 0xe7, 
            80, 7, 7, 0, 8, 0x5b, 0, 8, 0x1b, 0, 9, 0x97, 0x54, 7, 0x43, 0, 
            8, 0x7b, 0, 8, 0x3b, 0, 9, 0xd7, 0x52, 7, 0x13, 0, 8, 0x6b, 0, 8, 
            0x2b, 0, 9, 0xb7, 0, 8, 11, 0, 8, 0x8b, 0, 8, 0x4b, 0, 9, 0xf7, 
            80, 7, 5, 0, 8, 0x57, 0, 8, 0x17, 0xc0, 8, 0, 0x53, 7, 0x33, 0, 
            8, 0x77, 0, 8, 0x37, 0, 9, 0xcf, 0x51, 7, 15, 0, 8, 0x67, 0, 8, 
            0x27, 0, 9, 0xaf, 0, 8, 7, 0, 8, 0x87, 0, 8, 0x47, 0, 9, 0xef, 
            80, 7, 9, 0, 8, 0x5f, 0, 8, 0x1f, 0, 9, 0x9f, 0x54, 7, 0x63, 0, 
            8, 0x7f, 0, 8, 0x3f, 0, 9, 0xdf, 0x52, 7, 0x1b, 0, 8, 0x6f, 0, 8, 
            0x2f, 0, 9, 0xbf, 0, 8, 15, 0, 8, 0x8f, 0, 8, 0x4f, 0, 9, 0xff
         };
        internal int[] hn;
        private const int MANY = 0x5a0;
        internal int[] r;
        internal int[] u;
        internal int[] v;
        internal int[] x;
        private const int Z_BUF_ERROR = -5;
        private const int Z_DATA_ERROR = -3;
        private const int Z_ERRNO = -1;
        private const int Z_MEM_ERROR = -4;
        private const int Z_NEED_DICT = 2;
        private const int Z_OK = 0;
        private const int Z_STREAM_END = 1;
        private const int Z_STREAM_ERROR = -2;
        private const int Z_VERSION_ERROR = -6;

        private int huft_build(int[] b, int bindex, int n, int s, int[] d, int[] e, int[] t, int[] m, int[] hp, int[] hn, int[] v)
        {
            int index = 0;
            int num5 = n;
            do
            {
                this.c[b[bindex + index]]++;
                index++;
                num5--;
            }
            while (num5 != 0);
            if (this.c[0] == n)
            {
                t[0] = -1;
                m[0] = 0;
                return 0;
            }
            int num8 = m[0];
            int num6 = 1;
            while (num6 <= 15)
            {
                if (this.c[num6] != 0)
                {
                    break;
                }
                num6++;
            }
            int num7 = num6;
            if (num8 < num6)
            {
                num8 = num6;
            }
            num5 = 15;
            while (num5 != 0)
            {
                if (this.c[num5] != 0)
                {
                    break;
                }
                num5--;
            }
            int num3 = num5;
            if (num8 > num5)
            {
                num8 = num5;
            }
            m[0] = num8;
            int num14 = ((int) 1) << num6;
            while (num6 < num5)
            {
                num14 -= this.c[num6];
                if (num14 < 0)
                {
                    return -3;
                }
                num6++;
                num14 = num14 << 1;
            }
            num14 -= this.c[num5];
            if (num14 < 0)
            {
                return -3;
            }
            this.c[num5] += num14;
            this.x[1] = num6 = 0;
            index = 1;
            int num13 = 2;
            while (--num5 != 0)
            {
                this.x[num13] = num6 += this.c[index];
                num13++;
                index++;
            }
            num5 = 0;
            index = 0;
            do
            {
                num6 = b[bindex + index];
                if (num6 != 0)
                {
                    v[this.x[num6]++] = num5;
                }
                index++;
            }
            while (++num5 < n);
            n = this.x[num3];
            this.x[0] = num5 = 0;
            index = 0;
            int num4 = -1;
            int bits = -num8;
            this.u[0] = 0;
            int num11 = 0;
            int num15 = 0;
            while (num7 <= num3)
            {
                int num = this.c[num7];
                while (num-- != 0)
                {
                    int num2;
                    while (num7 > (bits + num8))
                    {
                        num4++;
                        bits += num8;
                        num15 = num3 - bits;
                        num15 = (num15 > num8) ? num8 : num15;
                        if ((num2 = ((int) 1) << (num6 = num7 - bits)) > (num + 1))
                        {
                            num2 -= num + 1;
                            num13 = num7;
                            if (num6 < num15)
                            {
                                while (++num6 < num15)
                                {
                                    if ((num2 = num2 << 1) <= this.c[++num13])
                                    {
                                        break;
                                    }
                                    num2 -= this.c[num13];
                                }
                            }
                        }
                        num15 = ((int) 1) << num6;
                        if ((hn[0] + num15) > 0x5a0)
                        {
                            return -3;
                        }
                        this.u[num4] = num11 = hn[0];
                        hn[0] += num15;
                        if (num4 != 0)
                        {
                            this.x[num4] = num5;
                            this.r[0] = (sbyte) num6;
                            this.r[1] = (sbyte) num8;
                            num6 = SharedUtils.URShift(num5, bits - num8);
                            this.r[2] = (num11 - this.u[num4 - 1]) - num6;
                            Array.Copy(this.r, 0, hp, (this.u[num4 - 1] + num6) * 3, 3);
                        }
                        else
                        {
                            t[0] = num11;
                        }
                    }
                    this.r[1] = (sbyte) (num7 - bits);
                    if (index >= n)
                    {
                        this.r[0] = 0xc0;
                    }
                    else if (v[index] < s)
                    {
                        this.r[0] = (v[index] < 0x100) ? ((sbyte) 0) : ((sbyte) 0x60);
                        this.r[2] = v[index++];
                    }
                    else
                    {
                        this.r[0] = (sbyte) ((e[v[index] - s] + 0x10) + 0x40);
                        this.r[2] = d[v[index++] - s];
                    }
                    num2 = ((int) 1) << (num7 - bits);
                    num6 = SharedUtils.URShift(num5, bits);
                    while (num6 < num15)
                    {
                        Array.Copy(this.r, 0, hp, (num11 + num6) * 3, 3);
                        num6 += num2;
                    }
                    num6 = ((int) 1) << (num7 - 1);
                    while ((num5 & num6) != 0)
                    {
                        num5 ^= num6;
                        num6 = SharedUtils.URShift(num6, 1);
                    }
                    num5 ^= num6;
                    for (int i = (((int) 1) << bits) - 1; (num5 & i) != this.x[num4]; i = (((int) 1) << bits) - 1)
                    {
                        num4--;
                        bits -= num8;
                    }
                }
                num7++;
            }
            return (((num14 != 0) && (num3 != 1)) ? -5 : 0);
        }

        internal int inflate_trees_bits(int[] c, int[] bb, int[] tb, int[] hp, ZlibCodec z)
        {
            this.initWorkArea(0x13);
            this.hn[0] = 0;
            int num = this.huft_build(c, 0, 0x13, 0x13, null, null, tb, bb, hp, this.hn, this.v);
            if (num == -3)
            {
                z.Message = "oversubscribed dynamic bit lengths tree";
                return num;
            }
            if ((num == -5) || (bb[0] == 0))
            {
                z.Message = "incomplete dynamic bit lengths tree";
                num = -3;
            }
            return num;
        }

        internal int inflate_trees_dynamic(int nl, int nd, int[] c, int[] bl, int[] bd, int[] tl, int[] td, int[] hp, ZlibCodec z)
        {
            this.initWorkArea(0x120);
            this.hn[0] = 0;
            int num = this.huft_build(c, 0, nl, 0x101, cplens, cplext, tl, bl, hp, this.hn, this.v);
            if ((num != 0) || (bl[0] == 0))
            {
                if (num == -3)
                {
                    z.Message = "oversubscribed literal/length tree";
                    return num;
                }
                if (num != -4)
                {
                    z.Message = "incomplete literal/length tree";
                    num = -3;
                }
                return num;
            }
            this.initWorkArea(0x120);
            num = this.huft_build(c, nl, nd, 0, cpdist, cpdext, td, bd, hp, this.hn, this.v);
            if ((num != 0) || ((bd[0] == 0) && (nl > 0x101)))
            {
                if (num == -3)
                {
                    z.Message = "oversubscribed distance tree";
                    return num;
                }
                if (num == -5)
                {
                    z.Message = "incomplete distance tree";
                    return -3;
                }
                if (num != -4)
                {
                    z.Message = "empty distance tree with lengths";
                    num = -3;
                }
                return num;
            }
            return 0;
        }

        internal static int inflate_trees_fixed(int[] bl, int[] bd, int[][] tl, int[][] td, ZlibCodec z)
        {
            bl[0] = 9;
            bd[0] = 5;
            tl[0] = fixed_tl;
            td[0] = fixed_td;
            return 0;
        }

        private void initWorkArea(int vsize)
        {
            if (this.hn == null)
            {
                this.hn = new int[1];
                this.v = new int[vsize];
                this.c = new int[0x10];
                this.r = new int[3];
                this.u = new int[15];
                this.x = new int[0x10];
            }
            else
            {
                if (this.v.Length < vsize)
                {
                    this.v = new int[vsize];
                }
                Array.Clear(this.v, 0, vsize);
                Array.Clear(this.c, 0, 0x10);
                this.r[0] = 0;
                this.r[1] = 0;
                this.r[2] = 0;
                Array.Clear(this.u, 0, 15);
                Array.Clear(this.x, 0, 0x10);
            }
        }
    }
}

