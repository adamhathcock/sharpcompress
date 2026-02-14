namespace SharpCompress.Compressors.LZMA.Utilities;

internal enum BlockType : byte
{
    #region Constants

    End = 0,
    Header = 1,
    ArchiveProperties = 2,
    AdditionalStreamsInfo = 3,
    MainStreamsInfo = 4,
    FilesInfo = 5,
    PackInfo = 6,
    UnpackInfo = 7,
    SubStreamsInfo = 8,
    Size = 9,
    Crc = 10,
    Folder = 11,
    CodersUnpackSize = 12,
    NumUnpackStream = 13,
    EmptyStream = 14,
    EmptyFile = 15,
    Anti = 16,
    Name = 17,
    CTime = 18,
    ATime = 19,
    MTime = 20,
    WinAttributes = 21,
    Comment = 22,
    EncodedHeader = 23,
    StartPos = 24,
    Dummy = 25

    #endregion
}
