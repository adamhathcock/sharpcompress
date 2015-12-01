namespace SharpCompress.Compressor.Rar.decode
{
    using System;

    internal class Compress
    {
        public const int BC = 20;
        public const int BC20 = 0x13;
        public const int CODEBUFSIZE = 0x4000;
        public const int DC = 60;
        public const int DC20 = 0x30;
        public static readonly int HUFF_TABLE_SIZE = 0x194;
        public const int LDC = 0x11;
        public const int LOW_DIST_REP_COUNT = 0x10;
        public static readonly int MAXWINMASK = 0x3fffff;
        public const int MAXWINSIZE = 0x400000;
        public const int MC20 = 0x101;
        public const int NC = 0x12b;
        public const int NC20 = 0x12a;
        public const int RC = 0x1c;
        public const int RC20 = 0x1c;
    }
}

