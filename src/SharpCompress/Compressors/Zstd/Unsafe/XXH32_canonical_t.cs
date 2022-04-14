using System;

namespace ZstdSharp.Unsafe
{
    /* **************************
    *  Canonical representation
    ****************************/
    /* Default result type for XXH functions are primitive unsigned 32 and 64 bits.
    *  The canonical representation uses human-readable write convention, aka big-endian (large digits first).
    *  These functions allow transformation of hash result into and from its canonical format.
    *  This way, hash values can be written into a file / memory, and remain comparable on different systems and programs.
    */
    public unsafe partial struct XXH32_canonical_t
    {
        public fixed byte digest[4];
    }
}
