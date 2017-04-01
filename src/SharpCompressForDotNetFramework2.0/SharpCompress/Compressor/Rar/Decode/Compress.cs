
namespace SharpCompress.Compressor.Rar.decode
{
    internal class Compress
    {
        public const int CODEBUFSIZE = 0x4000;
        public const int MAXWINSIZE = 0x400000;
        //UPGRADE_NOTE: Final was removed from the declaration of 'MAXWINMASK '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int MAXWINMASK = (MAXWINSIZE - 1);

        public const int LOW_DIST_REP_COUNT = 16;

        public const int NC = 299; /* alphabet = {0, 1, 2, ..., NC - 1} */
        public const int DC = 60;
        public const int LDC = 17;
        public const int RC = 28;
        //UPGRADE_NOTE: Final was removed from the declaration of 'HUFF_TABLE_SIZE '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int HUFF_TABLE_SIZE = (NC + DC + RC + LDC);
        public const int BC = 20;

        public const int NC20 = 298; /* alphabet = {0, 1, 2, ..., NC - 1} */
        public const int DC20 = 48;
        public const int RC20 = 28;
        public const int BC20 = 19;
        public const int MC20 = 257;
    }
}