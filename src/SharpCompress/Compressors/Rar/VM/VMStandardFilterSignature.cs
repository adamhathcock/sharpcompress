namespace SharpCompress.Compressors.Rar.VM
{
    internal class VMStandardFilterSignature
    {
        internal VMStandardFilterSignature(int length, uint crc, VMStandardFilters type)
        {
            Length = length;
            CRC = crc;
            Type = type;
        }

        internal int Length { get; private set; }

        internal uint CRC { get; private set; }

        internal VMStandardFilters Type { get; private set; }
    }
}