namespace SharpCompress.Common.Rar.Headers
{
    using System;

    internal enum HeaderType
    {
        ArchiveHeader = 0x73,
        AvHeader = 0x76,
        CommHeader = 0x75,
        EndArchiveHeader = 0x7b,
        FileHeader = 0x74,
        MarkHeader = 0x72,
        NewSubHeader = 0x7a,
        ProtectHeader = 120,
        SignHeader = 0x79,
        SubHeader = 0x77
    }
}

