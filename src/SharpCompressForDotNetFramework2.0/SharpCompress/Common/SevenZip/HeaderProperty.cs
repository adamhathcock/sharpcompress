
namespace SharpCompress.Common.SevenZip
{
    internal enum HeaderProperty
    {
        kEnd,
        kHeader,

        kArchiveProperties,

        kAdditionalStreamsInfo,
        kMainStreamsInfo,
        kFilesInfo,

        kPackInfo,
        kUnPackInfo,
        kSubStreamsInfo,

        kSize,
        kCRC,

        kFolder,

        kCodersUnPackSize,
        kNumUnPackStream,

        kEmptyStream,
        kEmptyFile,
        kAnti,

        kName,
        kCreationTime,
        kLastAccessTime,
        kLastWriteTime,
        kWinAttributes,
        kComment,

        kEncodedHeader,
    }
}
