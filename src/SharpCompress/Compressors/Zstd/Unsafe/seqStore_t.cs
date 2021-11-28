using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct seqStore_t
    {
        public seqDef_s* sequencesStart;

        /* ptr to end of sequences */
        public seqDef_s* sequences;

        public byte* litStart;

        /* ptr to end of literals */
        public byte* lit;

        public byte* llCode;

        public byte* mlCode;

        public byte* ofCode;

        public nuint maxNbSeq;

        public nuint maxNbLit;

        /* longLengthPos and longLengthType to allow us to represent either a single litLength or matchLength
             * in the seqStore that has a value larger than U16 (if it exists). To do so, we increment
             * the existing value of the litLength or matchLength by 0x10000.
             */
        public ZSTD_longLengthType_e longLengthType;

        /* Index of the sequence to apply long length modification to */
        public uint longLengthPos;
    }
}
