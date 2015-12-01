namespace SharpCompress.Common.Tar.Headers
{
    using System;

    internal enum EntryType : byte
    {
        BlockDevice = 0x34,
        CharDevice = 0x33,
        Directory = 0x35,
        Fifo = 0x36,
        File = 0,
        HardLink = 0x31,
        LongLink = 0x4b,
        LongName = 0x4c,
        OldFile = 0x30,
        SparseFile = 0x53,
        SymLink = 50,
        VolumeHeader = 0x56
    }
}

