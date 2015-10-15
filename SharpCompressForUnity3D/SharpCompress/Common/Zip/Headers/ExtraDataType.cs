namespace SharpCompress.Common.Zip.Headers
{
    using System;

    internal enum ExtraDataType : ushort
    {
        NotImplementedExtraData = 0xffff,
        UnicodePathExtraField = 0x7075,
        WinZipAes = 0x9901
    }
}

