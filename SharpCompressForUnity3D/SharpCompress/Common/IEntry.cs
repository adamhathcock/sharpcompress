namespace SharpCompress.Common
{
    using System;

    public interface IEntry
    {
        DateTime? ArchivedTime { get; }

        int? Attrib { get; }

        long CompressedSize { get; }

        SharpCompress.Common.CompressionType CompressionType { get; }

        long Crc { get; }

        DateTime? CreatedTime { get; }

        bool IsDirectory { get; }

        bool IsEncrypted { get; }

        bool IsSplit { get; }

        string Key { get; }

        DateTime? LastAccessedTime { get; }

        DateTime? LastModifiedTime { get; }

        long Size { get; }
    }
}

