namespace ZstdSharp.Unsafe
{
    public struct seq_t
    {
        public nuint litLength;
        public nuint matchLength;
        public nuint offset;
    }
}