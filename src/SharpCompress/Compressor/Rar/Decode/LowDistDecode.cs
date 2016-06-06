namespace SharpCompress.Compressor.Rar.decode
{
    internal class LowDistDecode : Decode
    {
        internal LowDistDecode()
            : base(new int[Compress.LDC])
        {
        }
    }
}