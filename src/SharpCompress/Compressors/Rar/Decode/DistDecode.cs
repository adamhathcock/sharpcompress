namespace SharpCompress.Compressors.Rar.Decode
{
    internal class DistDecode : Decode
    {
        internal DistDecode()
            : base(new int[Compress.DC])
        {
        }
    }
}