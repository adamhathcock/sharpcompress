namespace SharpCompress.Compressors.Rar.Decode
{
    internal static class PackDef
    {
        public const int CODEBUFSIZE = 0x4000;
        public const int MAXWINSIZE = 0x400000;

        public const int MAXWINMASK = (MAXWINSIZE - 1);

        public const int LOW_DIST_REP_COUNT = 16;

        public const int NC = 299; /* alphabet = {0, 1, 2, ..., NC - 1} */
        public const int DC = 60;
        public const int LDC = 17;
        public const int RC = 28;

        public const int HUFF_TABLE_SIZE = (NC + DC + RC + LDC);
        public const int BC = 20;

        public const int NC20 = 298; /* alphabet = {0, 1, 2, ..., NC - 1} */
        public const int DC20 = 48;
        public const int RC20 = 28;
        public const int BC20 = 19;
        public const int MC20 = 257;
    }
}