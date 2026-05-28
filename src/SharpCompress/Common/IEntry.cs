using System;
using SharpCompress.Common.Options;

namespace SharpCompress.Common;

public interface IEntry
{
    CompressionType CompressionType { get; }
    DateTime? ArchivedTime { get; }
    long CompressedSize { get; }
    long Crc { get; }
    DateTime? CreatedTime { get; }
    string? Key { get; }
    string? LinkTarget { get; }
    bool IsDirectory { get; }
    bool IsEncrypted { get; }
    bool IsSplitAfter { get; }
    bool IsSolid { get; }
    int VolumeIndexFirst { get; }
    int VolumeIndexLast { get; }
    DateTime? LastAccessedTime { get; }
    DateTime? LastModifiedTime { get; }
    long Size { get; }
    int? Attrib { get; }

    /// <summary>
    /// The options used when opening this entry's source (reader or archive).
    /// </summary>
    IReaderOptions Options { get; }
}
