namespace SharpCompress.Common.Zip.Headers
{
    using System;

    [Flags]
    internal enum HeaderFlags : ushort
    {
        Bit1 = 2,
        Bit2 = 4,
        Encrypted = 1,
        EnhancedDeflate = 0x10,
        UsePostDataDescriptor = 8,
        UTF8 = 0x800
    }
}

