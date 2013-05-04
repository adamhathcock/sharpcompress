using System;

namespace SharpCompress
{
    public interface IEntry
    {
        CompressionType CompressionType
        {
            get;
        }
        DateTimeOffset ArchivedTime
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
        DateTimeOffset CreatedTime
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
        DateTimeOffset LastAccessedTime
        {
            get;
        }
        DateTimeOffset LastModifiedTime
        {
            get;
        }
        long Size
        {
            get;
        }
    }
}