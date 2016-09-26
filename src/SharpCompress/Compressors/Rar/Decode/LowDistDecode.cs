namespace SharpCompress.Compressors.Rar.Decode
{
    internal class LowDistDecode : Decode
    {
        internal LowDistDecode()
            : base(new int[Compress.LDC])
        {
        }
    }
}