namespace SharpCompress.Compressor.LZMA.Utilites
{
    using System;

    internal enum BlockType : byte
    {
        AdditionalStreamsInfo = 3,
        Anti = 0x10,
        ArchiveProperties = 2,
        ATime = 0x13,
        CodersUnpackSize = 12,
        Comment = 0x16,
        CRC = 10,
        CTime = 0x12,
        Dummy = 0x19,
        EmptyFile = 15,
        EmptyStream = 14,
        EncodedHeader = 0x17,
        End = 0,
        FilesInfo = 5,
        Folder = 11,
        Header = 1,
        MainStreamsInfo = 4,
        MTime = 20,
        Name = 0x11,
        NumUnpackStream = 13,
        PackInfo = 6,
        Size = 9,
        StartPos = 0x18,
        SubStreamsInfo = 8,
        UnpackInfo = 7,
        WinAttributes = 0x15
    }
}

