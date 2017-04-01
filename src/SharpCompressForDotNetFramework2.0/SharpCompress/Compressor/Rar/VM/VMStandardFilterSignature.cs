namespace SharpCompress.Compressor.Rar.VM
{
    internal class VMStandardFilterSignature
    {
        internal VMStandardFilterSignature(int length, uint crc, VMStandardFilters type)
        {
            this.Length = length;
            CRC = crc;
            this.Type = type;
        }

        internal int Length
        {
            get;
            private set;
        }

        internal uint CRC
        {
            get;
            private set;
        }

        internal VMStandardFilters Type
        {
            get;
            private set;
        }
    }
}