namespace SharpCompress.Compressor.Rar.decode
{
    internal class DistDecode : Decode
    {
        internal DistDecode()
            : base(new int[Compress.DC])
        {
        }
    }
}