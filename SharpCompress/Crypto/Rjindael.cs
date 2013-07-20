using System;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;

namespace Org.BouncyCastle.Crypto.Engines
{
    /**
    * an implementation of Rijndael, based on the documentation and reference implementation
    * by Paulo Barreto, Vincent Rijmen, for v2.0 August '99.
    * <p>
    * Note: this implementation is based on information prior to readonly NIST publication.
    * </p>
    */
    public class RijndaelEngine
        : IBlockCipher
    {
        private static readonly int MAXROUNDS = 14;

        private static readonly int MAXKC = (256 / 4);

        private static readonly byte[] Logtable =
        {
            0,    0,    25,   1,    50,   2,    26,   198,
            75,   199,  27,   104,  51,   238,  223,  3,
            100,  4,    224,  14,   52,   141,  129,  239,
            76,   113,  8,    200,  248,  105,  28,   193,
            125,  194,  29,   181,  249,  185,  39,   106,
            77,   228,  166,  114,  154,  201,  9,    120,
            101,  47,   138,  5,    33,   15,   225,  36,
            18,   240,  130,  69,   53,   147,  218,  142,
            150,  143,  219,  189,  54,   208,  206,  148,
            19,   92,   210,  241,  64,   70,   131,  56,
            102,  221,  253,  48,   191,  6,    139,  98,
            179,  37,   226,  152,  34,   136,  145,  16,
            126,  110,  72,   195,  163,  182,  30,   66,
            58,   107,  40,   84,   250,  133,  61,   186,
            43,   121,  10,   21,   155,  159,  94,   202,
            78,   212,  172,  229,  243,  115,  167,  87,
            175,  88,   168,  80,   244,  234,  214,  116,
            79,   174,  233,  213,  231,  230,  173,  232,
            44,   215,  117,  122,  235,  22,   11,   245,
            89,   203,  95,   176,  156,  169,  81,   160,
            127,  12,   246,  111,  23,   196,  73,   236,
            216,  67,   31,   45,   164,  118,  123,  183,
            204,  187,  62,   90,   251,  96,   177,  134,
            59,   82,   161,  108,  170,  85,   41,   157,
            151,  178,  135,  144,  97,   190,  220,  252,
            188,  149,  207,  205,  55,   63,   91,   209,
            83,   57,   132,  60,   65,   162,  109,  71,
            20,   42,   158,  93,   86,   242,  211,  171,
            68,   17,   146,  217,  35,   32,   46,   137,
            180,  124,  184,  38,   119,  153,  227,  165,
            103,  74,   237,  222,  197,  49,   254,  24,
            13,   99,   140,  128,  192,  247,  112,  7
        };

        private static readonly byte[] Alogtable =
        {
            0,   3,   5,  15,  17,  51,  85, 255,  26,  46, 114, 150, 161, 248,  19,  53,
            95, 225,  56,  72, 216, 115, 149, 164, 247,   2,   6,  10,  30,  34, 102, 170,
            229,  52,  92, 228,  55,  89, 235,  38, 106, 190, 217, 112, 144, 171, 230,  49,
            83, 245,   4,  12,  20,  60,  68, 204,  79, 209, 104, 184, 211, 110, 178, 205,
            76, 212, 103, 169, 224,  59,  77, 215,  98, 166, 241,   8,  24,  40, 120, 136,
            131, 158, 185, 208, 107, 189, 220, 127, 129, 152, 179, 206,  73, 219, 118, 154,
            181, 196,  87, 249,  16,  48,  80, 240,  11,  29,  39, 105, 187, 214,  97, 163,
            254,  25,  43, 125, 135, 146, 173, 236,  47, 113, 147, 174, 233,  32,  96, 160,
            251,  22,  58,  78, 210, 109, 183, 194,  93, 231,  50,  86, 250,  21,  63,  65,
            195,  94, 226,  61,  71, 201,  64, 192,  91, 237,  44, 116, 156, 191, 218, 117,
            159, 186, 213, 100, 172, 239,  42, 126, 130, 157, 188, 223, 122, 142, 137, 128,
            155, 182, 193,  88, 232,  35, 101, 175, 234,  37, 111, 177, 200,  67, 197,  84,
            252,  31,  33,  99, 165, 244,   7,   9,  27,  45, 119, 153, 176, 203,  70, 202,
            69, 207,  74, 222, 121, 139, 134, 145, 168, 227,  62,  66, 198,  81, 243,  14,
            18,  54,  90, 238,  41, 123, 141, 140, 143, 138, 133, 148, 167, 242,  13,  23,
            57,  75, 221, 124, 132, 151, 162, 253,  28,  36, 108, 180, 199,  82, 246,   1,
            3,   5,  15,  17,  51,  85, 255,  26,  46, 114, 150, 161, 248,  19,  53,
            95, 225,  56,  72, 216, 115, 149, 164, 247,   2,   6,  10,  30,  34, 102, 170,
            229,  52,  92, 228,  55,  89, 235,  38, 106, 190, 217, 112, 144, 171, 230,  49,
            83, 245,   4,  12,  20,  60,  68, 204,  79, 209, 104, 184, 211, 110, 178, 205,
            76, 212, 103, 169, 224,  59,  77, 215,  98, 166, 241,   8,  24,  40, 120, 136,
            131, 158, 185, 208, 107, 189, 220, 127, 129, 152, 179, 206,  73, 219, 118, 154,
            181, 196,  87, 249,  16,  48,  80, 240,  11,  29,  39, 105, 187, 214,  97, 163,
            254,  25,  43, 125, 135, 146, 173, 236,  47, 113, 147, 174, 233,  32,  96, 160,
            251,  22,  58,  78, 210, 109, 183, 194,  93, 231,  50,  86, 250,  21,  63,  65,
            195,  94, 226,  61,  71, 201,  64, 192,  91, 237,  44, 116, 156, 191, 218, 117,
            159, 186, 213, 100, 172, 239,  42, 126, 130, 157, 188, 223, 122, 142, 137, 128,
            155, 182, 193,  88, 232,  35, 101, 175, 234,  37, 111, 177, 200,  67, 197,  84,
            252,  31,  33,  99, 165, 244,   7,   9,  27,  45, 119, 153, 176, 203,  70, 202,
            69, 207,  74, 222, 121, 139, 134, 145, 168, 227,  62,  66, 198,  81, 243,  14,
            18,  54,  90, 238,  41, 123, 141, 140, 143, 138, 133, 148, 167, 242,  13,  23,
            57,  75, 221, 124, 132, 151, 162, 253,  28,  36, 108, 180, 199,  82, 246,   1,
        };

        private static readonly byte[] S =
        {
            99, 124, 119, 123, 242, 107, 111, 197,  48,   1, 103,  43, 254, 215, 171, 118,
            202, 130, 201, 125, 250,  89,  71, 240, 173, 212, 162, 175, 156, 164, 114, 192,
            183, 253, 147,  38,  54,  63, 247, 204,  52, 165, 229, 241, 113, 216,  49,  21,
            4, 199,  35, 195,  24, 150,   5, 154,   7,  18, 128, 226, 235,  39, 178, 117,
            9, 131,  44,  26,  27, 110,  90, 160,  82,  59, 214, 179,  41, 227,  47, 132,
            83, 209,   0, 237,  32, 252, 177,  91, 106, 203, 190,  57,  74,  76,  88, 207,
            208, 239, 170, 251,  67,  77,  51, 133,  69, 249,   2, 127,  80,  60, 159, 168,
            81, 163,  64, 143, 146, 157,  56, 245, 188, 182, 218,  33,  16, 255, 243, 210,
            205,  12,  19, 236,  95, 151,  68,  23, 196, 167, 126,  61, 100,  93,  25, 115,
            96, 129,  79, 220,  34,  42, 144, 136,  70, 238, 184,  20, 222,  94,  11, 219,
            224,  50,  58,  10,  73,   6,  36,  92, 194, 211, 172,  98, 145, 149, 228, 121,
            231, 200,  55, 109, 141, 213,  78, 169, 108,  86, 244, 234, 101, 122, 174,   8,
            186, 120,  37,  46,  28, 166, 180, 198, 232, 221, 116,  31,  75, 189, 139, 138,
            112,  62, 181, 102,  72,   3, 246,  14,  97,  53,  87, 185, 134, 193,  29, 158,
            225, 248, 152,  17, 105, 217, 142, 148, 155,  30, 135, 233, 206,  85,  40, 223,
            140, 161, 137,  13, 191, 230,  66, 104,  65, 153,  45,  15, 176,  84, 187,  22,
        };

        private static readonly byte[] Si =
        {
            82,   9, 106, 213,  48,  54, 165,  56, 191,  64, 163, 158, 129, 243, 215, 251,
            124, 227,  57, 130, 155,  47, 255, 135,  52, 142,  67,  68, 196, 222, 233, 203,
            84, 123, 148,  50, 166, 194,  35,  61, 238,  76, 149,  11,  66, 250, 195,  78,
            8,  46, 161, 102,  40, 217,  36, 178, 118,  91, 162,  73, 109, 139, 209,  37,
            114, 248, 246, 100, 134, 104, 152,  22, 212, 164,  92, 204,  93, 101, 182, 146,
            108, 112,  72,  80, 253, 237, 185, 218,  94,  21,  70,  87, 167, 141, 157, 132,
            144, 216, 171,   0, 140, 188, 211,  10, 247, 228,  88,   5, 184, 179,  69,   6,
            208,  44,  30, 143, 202,  63,  15,   2, 193, 175, 189,   3,   1,  19, 138, 107,
            58, 145,  17,  65,  79, 103, 220, 234, 151, 242, 207, 206, 240, 180, 230, 115,
            150, 172, 116,  34, 231, 173,  53, 133, 226, 249,  55, 232,  28, 117, 223, 110,
            71, 241,  26, 113,  29,  41, 197, 137, 111, 183,  98,  14, 170,  24, 190,  27,
            252,  86,  62,  75, 198, 210, 121,  32, 154, 219, 192, 254, 120, 205,  90, 244,
            31, 221, 168,  51, 136,   7, 199,  49, 177,  18,  16,  89,  39, 128, 236,  95,
            96,  81, 127, 169,  25, 181,  74,  13,  45, 229, 122, 159, 147, 201, 156, 239,
            160, 224,  59,  77, 174,  42, 245, 176, 200, 235, 187,  60, 131,  83, 153,  97,
            23,  43,   4, 126, 186, 119, 214,  38, 225, 105,  20,  99,  85,  33,  12, 125,
        };

        private static readonly byte[] rcon =
        {
            0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36, 0x6c, 0xd8, 0xab, 0x4d, 0x9a,
            0x2f, 0x5e, 0xbc, 0x63, 0xc6, 0x97, 0x35, 0x6a, 0xd4, 0xb3, 0x7d, 0xfa, 0xef, 0xc5, 0x91
        };

        static readonly byte[][] shifts0 = new byte[][]
        {
            new byte[]{ 0, 8, 16, 24 },
            new byte[]{ 0, 8, 16, 24 },
            new byte[]{ 0, 8, 16, 24 },
            new byte[]{ 0, 8, 16, 32 },
            new byte[]{ 0, 8, 24, 32 }
        };

        static readonly byte[][] shifts1 =
        {
            new byte[]{ 0, 24, 16, 8 },
            new byte[]{ 0, 32, 24, 16 },
            new byte[]{ 0, 40, 32, 24 },
            new byte[]{ 0, 48, 40, 24 },
            new byte[]{ 0, 56, 40, 32 }
        };

        /**
        * multiply two elements of GF(2^m)
        * needed for MixColumn and InvMixColumn
        */
        private byte Mul0x2(
            int b)
        {
            if (b != 0)
            {
                return Alogtable[25 + (Logtable[b] & 0xff)];
            }
            else
            {
                return 0;
            }
        }

        private byte Mul0x3(
            int b)
        {
            if (b != 0)
            {
                return Alogtable[1 + (Logtable[b] & 0xff)];
            }
            else
            {
                return 0;
            }
        }

        private byte Mul0x9(
            int b)
        {
            if (b >= 0)
            {
                return Alogtable[199 + b];
            }
            else
            {
                return 0;
            }
        }

        private byte Mul0xb(
            int b)
        {
            if (b >= 0)
            {
                return Alogtable[104 + b];
            }
            else
            {
                return 0;
            }
        }

        private byte Mul0xd(
            int b)
        {
            if (b >= 0)
            {
                return Alogtable[238 + b];
            }
            else
            {
                return 0;
            }
        }

        private byte Mul0xe(
            int b)
        {
            if (b >= 0)
            {
                return Alogtable[223 + b];
            }
            else
            {
                return 0;
            }
        }

        /**
        * xor corresponding text input and round key input bytes
        */
        private void KeyAddition(
            long[] rk)
        {
            A0 ^= rk[0];
            A1 ^= rk[1];
            A2 ^= rk[2];
            A3 ^= rk[3];
        }

        private long Shift(
            long r,
            int shift)
        {
            //return (((long)((ulong) r >> shift) | (r << (BC - shift)))) & BC_MASK;

            ulong temp = (ulong)r >> shift;

            // NB: This corrects for Mono Bug #79087 (fixed in 1.1.17)
            if (shift > 31)
            {
                temp &= 0xFFFFFFFFUL;
            }

            return ((long)temp | (r << (BC - shift))) & BC_MASK;
        }

        /**
        * Row 0 remains unchanged
        * The other three rows are shifted a variable amount
        */
        private void ShiftRow(
            byte[] shiftsSC)
        {
            A1 = Shift(A1, shiftsSC[1]);
            A2 = Shift(A2, shiftsSC[2]);
            A3 = Shift(A3, shiftsSC[3]);
        }

        private long ApplyS(
            long r,
            byte[] box)
        {
            long res = 0;

            for (int j = 0; j < BC; j += 8)
            {
                res |= (long)(box[(int)((r >> j) & 0xff)] & 0xff) << j;
            }

            return res;
        }

        /**
        * Replace every byte of the input by the byte at that place
        * in the nonlinear S-box
        */
        private void Substitution(
            byte[] box)
        {
            A0 = ApplyS(A0, box);
            A1 = ApplyS(A1, box);
            A2 = ApplyS(A2, box);
            A3 = ApplyS(A3, box);
        }

        /**
        * Mix the bytes of every column in a linear way
        */
        private void MixColumn()
        {
            long r0, r1, r2, r3;

            r0 = r1 = r2 = r3 = 0;

            for (int j = 0; j < BC; j += 8)
            {
                int a0 = (int)((A0 >> j) & 0xff);
                int a1 = (int)((A1 >> j) & 0xff);
                int a2 = (int)((A2 >> j) & 0xff);
                int a3 = (int)((A3 >> j) & 0xff);

                r0 |= (long)((Mul0x2(a0) ^ Mul0x3(a1) ^ a2 ^ a3) & 0xff) << j;

                r1 |= (long)((Mul0x2(a1) ^ Mul0x3(a2) ^ a3 ^ a0) & 0xff) << j;

                r2 |= (long)((Mul0x2(a2) ^ Mul0x3(a3) ^ a0 ^ a1) & 0xff) << j;

                r3 |= (long)((Mul0x2(a3) ^ Mul0x3(a0) ^ a1 ^ a2) & 0xff) << j;
            }

            A0 = r0;
            A1 = r1;
            A2 = r2;
            A3 = r3;
        }

        /**
        * Mix the bytes of every column in a linear way
        * This is the opposite operation of Mixcolumn
        */
        private void InvMixColumn()
        {
            long r0, r1, r2, r3;

            r0 = r1 = r2 = r3 = 0;
            for (int j = 0; j < BC; j += 8)
            {
                int a0 = (int)((A0 >> j) & 0xff);
                int a1 = (int)((A1 >> j) & 0xff);
                int a2 = (int)((A2 >> j) & 0xff);
                int a3 = (int)((A3 >> j) & 0xff);

                //
                // pre-lookup the log table
                //
                a0 = (a0 != 0) ? (Logtable[a0 & 0xff] & 0xff) : -1;
                a1 = (a1 != 0) ? (Logtable[a1 & 0xff] & 0xff) : -1;
                a2 = (a2 != 0) ? (Logtable[a2 & 0xff] & 0xff) : -1;
                a3 = (a3 != 0) ? (Logtable[a3 & 0xff] & 0xff) : -1;

                r0 |= (long)((Mul0xe(a0) ^ Mul0xb(a1) ^ Mul0xd(a2) ^ Mul0x9(a3)) & 0xff) << j;

                r1 |= (long)((Mul0xe(a1) ^ Mul0xb(a2) ^ Mul0xd(a3) ^ Mul0x9(a0)) & 0xff) << j;

                r2 |= (long)((Mul0xe(a2) ^ Mul0xb(a3) ^ Mul0xd(a0) ^ Mul0x9(a1)) & 0xff) << j;

                r3 |= (long)((Mul0xe(a3) ^ Mul0xb(a0) ^ Mul0xd(a1) ^ Mul0x9(a2)) & 0xff) << j;
            }

            A0 = r0;
            A1 = r1;
            A2 = r2;
            A3 = r3;
        }

        /**
        * Calculate the necessary round keys
        * The number of calculations depends on keyBits and blockBits
        */
        private long[][] GenerateWorkingKey(
            byte[] key)
        {
            int KC;
            int t, rconpointer = 0;
            int keyBits = key.Length * 8;
            byte[,] tk = new byte[4, MAXKC];
            //long[,]    W = new long[MAXROUNDS+1,4];
            long[][] W = new long[MAXROUNDS + 1][];

            for (int i = 0; i < MAXROUNDS + 1; i++)
                W[i] = new long[4];

            switch (keyBits)
            {
                case 128:
                    KC = 4;
                    break;
                case 160:
                    KC = 5;
                    break;
                case 192:
                    KC = 6;
                    break;
                case 224:
                    KC = 7;
                    break;
                case 256:
                    KC = 8;
                    break;
                default:
                    throw new ArgumentException("Key length not 128/160/192/224/256 bits.");
            }

            if (keyBits >= blockBits)
            {
                ROUNDS = KC + 6;
            }
            else
            {
                ROUNDS = (BC / 8) + 6;
            }

            //
            // copy the key into the processing area
            //
            int index = 0;

            for (int i = 0; i < key.Length; i++)
            {
                tk[i % 4, i / 4] = key[index++];
            }

            t = 0;

            //
            // copy values into round key array
            //
            for (int j = 0; (j < KC) && (t < (ROUNDS + 1) * (BC / 8)); j++, t++)
            {
                for (int i = 0; i < 4; i++)
                {
                    W[t / (BC / 8)][i] |= (long)(tk[i, j] & 0xff) << ((t * 8) % BC);
                }
            }

            //
            // while not enough round key material calculated
            // calculate new values
            //
            while (t < (ROUNDS + 1) * (BC / 8))
            {
                for (int i = 0; i < 4; i++)
                {
                    tk[i, 0] ^= S[tk[(i + 1) % 4, KC - 1] & 0xff];
                }
                tk[0, 0] ^= (byte)rcon[rconpointer++];

                if (KC <= 6)
                {
                    for (int j = 1; j < KC; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            tk[i, j] ^= tk[i, j - 1];
                        }
                    }
                }
                else
                {
                    for (int j = 1; j < 4; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            tk[i, j] ^= tk[i, j - 1];
                        }
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        tk[i, 4] ^= S[tk[i, 3] & 0xff];
                    }
                    for (int j = 5; j < KC; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            tk[i, j] ^= tk[i, j - 1];
                        }
                    }
                }

                //
                // copy values into round key array
                //
                for (int j = 0; (j < KC) && (t < (ROUNDS + 1) * (BC / 8)); j++, t++)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        W[t / (BC / 8)][i] |= (long)(tk[i, j] & 0xff) << ((t * 8) % (BC));
                    }
                }
            }
            return W;
        }

        private int BC;
        private long BC_MASK;
        private int ROUNDS;
        private int blockBits;
        private long[][] workingKey;
        private long A0, A1, A2, A3;
        private bool forEncryption;
        private byte[] shifts0SC;
        private byte[] shifts1SC;

        /**
        * default constructor - 128 bit block size.
        */
        public RijndaelEngine() : this(128) { }

        /**
        * basic constructor - set the cipher up for a given blocksize
        *
        * @param blocksize the blocksize in bits, must be 128, 192, or 256.
        */
        public RijndaelEngine(
            int blockBits)
        {
            switch (blockBits)
            {
                case 128:
                    BC = 32;
                    BC_MASK = 0xffffffffL;
                    shifts0SC = shifts0[0];
                    shifts1SC = shifts1[0];
                    break;
                case 160:
                    BC = 40;
                    BC_MASK = 0xffffffffffL;
                    shifts0SC = shifts0[1];
                    shifts1SC = shifts1[1];
                    break;
                case 192:
                    BC = 48;
                    BC_MASK = 0xffffffffffffL;
                    shifts0SC = shifts0[2];
                    shifts1SC = shifts1[2];
                    break;
                case 224:
                    BC = 56;
                    BC_MASK = 0xffffffffffffffL;
                    shifts0SC = shifts0[3];
                    shifts1SC = shifts1[3];
                    break;
                case 256:
                    BC = 64;
                    BC_MASK = unchecked((long)0xffffffffffffffffL);
                    shifts0SC = shifts0[4];
                    shifts1SC = shifts1[4];
                    break;
                default:
                    throw new ArgumentException("unknown blocksize to Rijndael");
            }

            this.blockBits = blockBits;
        }

        /**
        * initialise a Rijndael cipher.
        *
        * @param forEncryption whether or not we are for encryption.
        * @param parameters the parameters required to set up the cipher.
        * @exception ArgumentException if the parameters argument is
        * inappropriate.
        */
        public void Init(
            bool forEncryption,
            KeyParameter parameters)
        {
            workingKey = GenerateWorkingKey(parameters.GetKey());
            this.forEncryption = forEncryption;
        }

        public string AlgorithmName
        {
            get { return "Rijndael"; }
        }

        public bool IsPartialBlockOkay
        {
            get { return false; }
        }

        public int GetBlockSize()
        {
            return BC / 2;
        }

        public int ProcessBlock(
            byte[] input,
            int inOff,
            byte[] output,
            int outOff)
        {
            if (workingKey == null)
            {
                throw new InvalidOperationException("Rijndael engine not initialised");
            }

            if ((inOff + (BC / 2)) > input.Length)
            {
                throw new DataLengthException("input buffer too short");
            }

            if ((outOff + (BC / 2)) > output.Length)
            {
                throw new DataLengthException("output buffer too short");
            }

            UnPackBlock(input, inOff);

            if (forEncryption)
            {
                EncryptBlock(workingKey);
            }
            else
            {
                DecryptBlock(workingKey);
            }

            PackBlock(output, outOff);

            return BC / 2;
        }

        public void Reset()
        {
        }

        private void UnPackBlock(
            byte[] bytes,
            int off)
        {
            int index = off;

            A0 = (long)(bytes[index++] & 0xff);
            A1 = (long)(bytes[index++] & 0xff);
            A2 = (long)(bytes[index++] & 0xff);
            A3 = (long)(bytes[index++] & 0xff);

            for (int j = 8; j != BC; j += 8)
            {
                A0 |= (long)(bytes[index++] & 0xff) << j;
                A1 |= (long)(bytes[index++] & 0xff) << j;
                A2 |= (long)(bytes[index++] & 0xff) << j;
                A3 |= (long)(bytes[index++] & 0xff) << j;
            }
        }

        private void PackBlock(
            byte[] bytes,
            int off)
        {
            int index = off;

            for (int j = 0; j != BC; j += 8)
            {
                bytes[index++] = (byte)(A0 >> j);
                bytes[index++] = (byte)(A1 >> j);
                bytes[index++] = (byte)(A2 >> j);
                bytes[index++] = (byte)(A3 >> j);
            }
        }

        private void EncryptBlock(
            long[][] rk)
        {
            int r;

            //
            // begin with a key addition
            //
            KeyAddition(rk[0]);

            //
            // ROUNDS-1 ordinary rounds
            //
            for (r = 1; r < ROUNDS; r++)
            {
                Substitution(S);
                ShiftRow(shifts0SC);
                MixColumn();
                KeyAddition(rk[r]);
            }

            //
            // Last round is special: there is no MixColumn
            //
            Substitution(S);
            ShiftRow(shifts0SC);
            KeyAddition(rk[ROUNDS]);
        }

        private void DecryptBlock(
            long[][] rk)
        {
            int r;

            // To decrypt: apply the inverse operations of the encrypt routine,
            //             in opposite order
            //
            // (KeyAddition is an involution: it 's equal to its inverse)
            // (the inverse of Substitution with table S is Substitution with the inverse table of S)
            // (the inverse of Shiftrow is Shiftrow over a suitable distance)
            //

            // First the special round:
            //   without InvMixColumn
            //   with extra KeyAddition
            //
            KeyAddition(rk[ROUNDS]);
            Substitution(Si);
            ShiftRow(shifts1SC);

            //
            // ROUNDS-1 ordinary rounds
            //
            for (r = ROUNDS - 1; r > 0; r--)
            {
                KeyAddition(rk[r]);
                InvMixColumn();
                Substitution(Si);
                ShiftRow(shifts1SC);
            }

            //
            // End with the extra key addition
            //
            KeyAddition(rk[0]);
        }
    }

}


namespace Org.BouncyCastle.Crypto
{

    /**
     * interface that a message digest conforms to.
     */
    public interface IDigest
    {
        /**
         * return the algorithm name
         *
         * @return the algorithm name
         */
        string AlgorithmName { get; }

        /**
         * return the size, in bytes, of the digest produced by this message digest.
         *
         * @return the size, in bytes, of the digest produced by this message digest.
         */
        int GetDigestSize();

        /**
         * return the size, in bytes, of the internal buffer used by this digest.
         *
         * @return the size, in bytes, of the internal buffer used by this digest.
         */
        int GetByteLength();

        /**
         * update the message digest with a single byte.
         *
         * @param inByte the input byte to be entered.
         */
        void Update(byte input);

        /**
         * update the message digest with a block of bytes.
         *
         * @param input the byte array containing the data.
         * @param inOff the offset into the byte array where the data starts.
         * @param len the length of the data.
         */
        void BlockUpdate(byte[] input, int inOff, int length);

        /**
         * Close the digest, producing the final digest value. The doFinal
         * call leaves the digest reset.
         *
         * @param output the array the digest is to be copied into.
         * @param outOff the offset into the out array the digest is to start at.
         */
        int DoFinal(byte[] output, int outOff);

        /**
         * reset the digest back to it's initial state.
         */
        void Reset();
    }

    /// <remarks>Base interface for a symmetric key block cipher.</remarks>
    public interface IBlockCipher
    {
        /// <summary>The name of the algorithm this cipher implements.</summary>
        string AlgorithmName { get; }

        /// <summary>Initialise the cipher.</summary>
        /// <param name="forEncryption">Initialise for encryption if true, for decryption if false.</param>
        /// <param name="parameters">The key or other data required by the cipher.</param>
        void Init(bool forEncryption, KeyParameter parameters);

        /// <returns>The block size for this cipher, in bytes.</returns>
        int GetBlockSize();

        /// <summary>Indicates whether this cipher can handle partial blocks.</summary>
        bool IsPartialBlockOkay { get; }

        /// <summary>Process a block.</summary>
        /// <param name="inBuf">The input buffer.</param>
        /// <param name="inOff">The offset into <paramref>inBuf</paramref> that the input block begins.</param>
        /// <param name="outBuf">The output buffer.</param>
        /// <param name="outOff">The offset into <paramref>outBuf</paramref> to write the output block.</param>
        /// <exception cref="DataLengthException">If input block is wrong size, or outBuf too small.</exception>
        /// <returns>The number of bytes processed and produced.</returns>
        int ProcessBlock(byte[] inBuf, int inOff, byte[] outBuf, int outOff);

        /// <summary>
        /// Reset the cipher to the same state as it was after the last init (if there was one).
        /// </summary>
        void Reset();
    }

    /**
 * all parameter classes implement this.
 */
    public interface ICipherParameters
    {
    }

    /**
 * this exception is thrown if a buffer that is meant to have output
 * copied into it turns out to be too short, or if we've been given
 * insufficient input. In general this exception will Get thrown rather
 * than an ArrayOutOfBounds exception.
 */
    public class DataLengthException
        : CryptoException
    {
        /**
        * base constructor.
        */
        public DataLengthException()
        {
        }

        /**
         * create a DataLengthException with the given message.
         *
         * @param message the message to be carried with the exception.
         */
        public DataLengthException(
            string message)
            : base(message)
        {
        }

        public DataLengthException(
            string message,
            Exception exception)
            : base(message, exception)
        {
        }
    }

    public class CryptoException
    : Exception
    {
        public CryptoException()
        {
        }

        public CryptoException(
            string message)
            : base(message)
        {
        }

        public CryptoException(
            string message,
            Exception exception)
            : base(message, exception)
        {
        }
    }
}

namespace Org.BouncyCastle.Crypto.Parameters
{
    public class KeyParameter
        : ICipherParameters
    {
        private readonly byte[] key;

        public KeyParameter(
            byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            this.key = (byte[])key.Clone();
        }

        public KeyParameter(
            byte[] key,
            int keyOff,
            int keyLen)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            if (keyOff < 0 || keyOff > key.Length)
                throw new ArgumentOutOfRangeException("keyOff");
            if (keyLen < 0 || (keyOff + keyLen) > key.Length)
                throw new ArgumentOutOfRangeException("keyLen");

            this.key = new byte[keyLen];
            Array.Copy(key, keyOff, this.key, 0, keyLen);
        }

        public byte[] GetKey()
        {
            return (byte[])key.Clone();
        }
    }

}

namespace Org.BouncyCastle.Crypto.Digests
{

    /**
     * implementation of SHA-1 as outlined in "Handbook of Applied Cryptography", pages 346 - 349.
     *
     * It is interesting to ponder why the, apart from the extra IV, the other difference here from MD5
     * is the "endienness" of the word processing!
     */
    public class Sha1Digest
        : GeneralDigest
    {
        private const int DigestLength = 20;

        private uint H1, H2, H3, H4, H5;

        private uint[] X = new uint[80];
        private int xOff;

        public Sha1Digest()
        {
            Reset();
        }

        /**
         * Copy constructor.  This will copy the state of the provided
         * message digest.
         */
        public Sha1Digest(Sha1Digest t)
            : base(t)
        {
            H1 = t.H1;
            H2 = t.H2;
            H3 = t.H3;
            H4 = t.H4;
            H5 = t.H5;

            Array.Copy(t.X, 0, X, 0, t.X.Length);
            xOff = t.xOff;
        }

        public override string AlgorithmName
        {
            get { return "SHA-1"; }
        }

        public override int GetDigestSize()
        {
            return DigestLength;
        }

        internal override void ProcessWord(
            byte[] input,
            int inOff)
        {
            X[xOff] = Pack.BE_To_UInt32(input, inOff);

            if (++xOff == 16)
            {
                ProcessBlock();
            }
        }

        internal override void ProcessLength(long bitLength)
        {
            if (xOff > 14)
            {
                ProcessBlock();
            }

            X[14] = (uint)((ulong)bitLength >> 32);
            X[15] = (uint)((ulong)bitLength);
        }

        public override int DoFinal(
            byte[] output,
            int outOff)
        {
            Finish();

            Pack.UInt32_To_BE(H1, output, outOff);
            Pack.UInt32_To_BE(H2, output, outOff + 4);
            Pack.UInt32_To_BE(H3, output, outOff + 8);
            Pack.UInt32_To_BE(H4, output, outOff + 12);
            Pack.UInt32_To_BE(H5, output, outOff + 16);

            Reset();

            return DigestLength;
        }

        /**
         * reset the chaining variables
         */
        public override void Reset()
        {
            base.Reset();

            H1 = 0x67452301;
            H2 = 0xefcdab89;
            H3 = 0x98badcfe;
            H4 = 0x10325476;
            H5 = 0xc3d2e1f0;

            xOff = 0;
            Array.Clear(X, 0, X.Length);
        }

        //
        // Additive constants
        //
        private const uint Y1 = 0x5a827999;
        private const uint Y2 = 0x6ed9eba1;
        private const uint Y3 = 0x8f1bbcdc;
        private const uint Y4 = 0xca62c1d6;

        private static uint F(uint u, uint v, uint w)
        {
            return (u & v) | (~u & w);
        }

        private static uint H(uint u, uint v, uint w)
        {
            return u ^ v ^ w;
        }

        private static uint G(uint u, uint v, uint w)
        {
            return (u & v) | (u & w) | (v & w);
        }

        internal override void ProcessBlock()
        {
            //
            // expand 16 word block into 80 word block.
            //
            for (int i = 16; i < 80; i++)
            {
                uint t = X[i - 3] ^ X[i - 8] ^ X[i - 14] ^ X[i - 16];
                X[i] = t << 1 | t >> 31;
            }

            //
            // set up working variables.
            //
            uint A = H1;
            uint B = H2;
            uint C = H3;
            uint D = H4;
            uint E = H5;

            //
            // round 1
            //
            int idx = 0;

            for (int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + F(B, C, D) + E + X[idx++] + Y1
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + F(B, C, D) + X[idx++] + Y1;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + F(A, B, C) + X[idx++] + Y1;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + F(E, A, B) + X[idx++] + Y1;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + F(D, E, A) + X[idx++] + Y1;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + F(C, D, E) + X[idx++] + Y1;
                C = C << 30 | (C >> 2);
            }

            //
            // round 2
            //
            for (int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + H(B, C, D) + E + X[idx++] + Y2
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + H(B, C, D) + X[idx++] + Y2;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + H(A, B, C) + X[idx++] + Y2;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + H(E, A, B) + X[idx++] + Y2;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + H(D, E, A) + X[idx++] + Y2;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + H(C, D, E) + X[idx++] + Y2;
                C = C << 30 | (C >> 2);
            }

            //
            // round 3
            //
            for (int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + G(B, C, D) + E + X[idx++] + Y3
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + G(B, C, D) + X[idx++] + Y3;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + G(A, B, C) + X[idx++] + Y3;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + G(E, A, B) + X[idx++] + Y3;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + G(D, E, A) + X[idx++] + Y3;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + G(C, D, E) + X[idx++] + Y3;
                C = C << 30 | (C >> 2);
            }

            //
            // round 4
            //
            for (int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + H(B, C, D) + E + X[idx++] + Y4
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + H(B, C, D) + X[idx++] + Y4;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + H(A, B, C) + X[idx++] + Y4;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + H(E, A, B) + X[idx++] + Y4;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + H(D, E, A) + X[idx++] + Y4;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + H(C, D, E) + X[idx++] + Y4;
                C = C << 30 | (C >> 2);
            }

            H1 += A;
            H2 += B;
            H3 += C;
            H4 += D;
            H5 += E;

            //
            // reset start of the buffer.
            //
            xOff = 0;
            Array.Clear(X, 0, 16);
        }
    }

    /**
* base implementation of MD4 family style digest as outlined in
* "Handbook of Applied Cryptography", pages 344 - 347.
*/
    public abstract class GeneralDigest
        : IDigest
    {
        private const int BYTE_LENGTH = 64;

        private byte[] xBuf;
        private int xBufOff;

        private long byteCount;

        internal GeneralDigest()
        {
            xBuf = new byte[4];
        }

        internal GeneralDigest(GeneralDigest t)
        {
            xBuf = new byte[t.xBuf.Length];
            Array.Copy(t.xBuf, 0, xBuf, 0, t.xBuf.Length);

            xBufOff = t.xBufOff;
            byteCount = t.byteCount;
        }

        public void Update(byte input)
        {
            xBuf[xBufOff++] = input;

            if (xBufOff == xBuf.Length)
            {
                ProcessWord(xBuf, 0);
                xBufOff = 0;
            }

            byteCount++;
        }

        public void BlockUpdate(
            byte[] input,
            int inOff,
            int length)
        {
            //
            // fill the current word
            //
            while ((xBufOff != 0) && (length > 0))
            {
                Update(input[inOff]);
                inOff++;
                length--;
            }

            //
            // process whole words.
            //
            while (length > xBuf.Length)
            {
                ProcessWord(input, inOff);

                inOff += xBuf.Length;
                length -= xBuf.Length;
                byteCount += xBuf.Length;
            }

            //
            // load in the remainder.
            //
            while (length > 0)
            {
                Update(input[inOff]);

                inOff++;
                length--;
            }
        }

        public void Finish()
        {
            long bitLength = (byteCount << 3);

            //
            // add the pad bytes.
            //
            Update((byte)128);

            while (xBufOff != 0)
                Update((byte)0);
            ProcessLength(bitLength);
            ProcessBlock();
        }

        public virtual void Reset()
        {
            byteCount = 0;
            xBufOff = 0;
            Array.Clear(xBuf, 0, xBuf.Length);
        }

        public int GetByteLength()
        {
            return BYTE_LENGTH;
        }

        internal abstract void ProcessWord(byte[] input, int inOff);
        internal abstract void ProcessLength(long bitLength);
        internal abstract void ProcessBlock();
        public abstract string AlgorithmName { get; }
        public abstract int GetDigestSize();
        public abstract int DoFinal(byte[] output, int outOff);
    }
}

namespace Org.BouncyCastle.Crypto.Utilities
{
    internal sealed class Pack
    {
        private Pack()
        {
        }

        internal static void UInt32_To_BE(uint n, byte[] bs)
        {
            bs[0] = (byte)(n >> 24);
            bs[1] = (byte)(n >> 16);
            bs[2] = (byte)(n >> 8);
            bs[3] = (byte)(n);
        }

        internal static void UInt32_To_BE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte)(n >> 24);
            bs[++off] = (byte)(n >> 16);
            bs[++off] = (byte)(n >> 8);
            bs[++off] = (byte)(n);
        }

        internal static uint BE_To_UInt32(byte[] bs)
        {
            uint n = (uint)bs[0] << 24;
            n |= (uint)bs[1] << 16;
            n |= (uint)bs[2] << 8;
            n |= (uint)bs[3];
            return n;
        }

        internal static uint BE_To_UInt32(byte[] bs, int off)
        {
            uint n = (uint)bs[off] << 24;
            n |= (uint)bs[++off] << 16;
            n |= (uint)bs[++off] << 8;
            n |= (uint)bs[++off];
            return n;
        }

        internal static ulong BE_To_UInt64(byte[] bs)
        {
            uint hi = BE_To_UInt32(bs);
            uint lo = BE_To_UInt32(bs, 4);
            return ((ulong)hi << 32) | (ulong)lo;
        }

        internal static ulong BE_To_UInt64(byte[] bs, int off)
        {
            uint hi = BE_To_UInt32(bs, off);
            uint lo = BE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | (ulong)lo;
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs)
        {
            UInt32_To_BE((uint)(n >> 32), bs);
            UInt32_To_BE((uint)(n), bs, 4);
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs, int off)
        {
            UInt32_To_BE((uint)(n >> 32), bs, off);
            UInt32_To_BE((uint)(n), bs, off + 4);
        }

        internal static void UInt32_To_LE(uint n, byte[] bs)
        {
            bs[0] = (byte)(n);
            bs[1] = (byte)(n >> 8);
            bs[2] = (byte)(n >> 16);
            bs[3] = (byte)(n >> 24);
        }

        internal static void UInt32_To_LE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte)(n);
            bs[++off] = (byte)(n >> 8);
            bs[++off] = (byte)(n >> 16);
            bs[++off] = (byte)(n >> 24);
        }

        internal static uint LE_To_UInt32(byte[] bs)
        {
            uint n = (uint)bs[0];
            n |= (uint)bs[1] << 8;
            n |= (uint)bs[2] << 16;
            n |= (uint)bs[3] << 24;
            return n;
        }

        internal static uint LE_To_UInt32(byte[] bs, int off)
        {
            uint n = (uint)bs[off];
            n |= (uint)bs[++off] << 8;
            n |= (uint)bs[++off] << 16;
            n |= (uint)bs[++off] << 24;
            return n;
        }

        internal static ulong LE_To_UInt64(byte[] bs)
        {
            uint lo = LE_To_UInt32(bs);
            uint hi = LE_To_UInt32(bs, 4);
            return ((ulong)hi << 32) | (ulong)lo;
        }

        internal static ulong LE_To_UInt64(byte[] bs, int off)
        {
            uint lo = LE_To_UInt32(bs, off);
            uint hi = LE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | (ulong)lo;
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs)
        {
            UInt32_To_LE((uint)(n), bs);
            UInt32_To_LE((uint)(n >> 32), bs, 4);
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs, int off)
        {
            UInt32_To_LE((uint)(n), bs, off);
            UInt32_To_LE((uint)(n >> 32), bs, off + 4);
        }
    }
}