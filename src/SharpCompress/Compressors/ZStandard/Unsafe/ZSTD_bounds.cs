namespace ZstdSharp.Unsafe
{
    public struct ZSTD_bounds
    {
        public nuint error;
        public int lowerBound;
        public int upperBound;
    }
}