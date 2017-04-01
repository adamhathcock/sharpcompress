using System;

namespace SharpCompress.Common
{
    public interface IEntry
    {
        CompressionType CompressionType
        {
            get;
        }
        DateTime? ArchivedTime
        {
            get;
        }
        long CompressedSize
        {
            get;
        }
        uint Crc
        {
            get;
        }
        DateTime? CreatedTime
        {
            get;
        }
        string FilePath
        {
            get;
        }
        bool IsDirectory
        {
            get;
        }
        bool IsEncrypted
        {
            get;
        }
        bool IsSplit
        {
            get;
        }
        DateTime? LastAccessedTime
        {
            get;
        }
        DateTime? LastModifiedTime
        {
            get;
        }
        long Size
        {
            get;
        }
    }
}
