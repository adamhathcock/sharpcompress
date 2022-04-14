using System;

namespace ZstdSharp.Unsafe
{
    public partial struct seq_t
    {
        public nuint litLength;

        public nuint matchLength;

        public nuint offset;
    }
}
