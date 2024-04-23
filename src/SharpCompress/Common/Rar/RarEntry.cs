using System;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Common.Rar;

public abstract class RarEntry : Entry
{
    internal abstract FileHeader FileHeader { get; }

    /// <summary>
    /// As the V2017 port isn't complete, add this check to use the legacy Rar code.
    /// </summary>
    internal bool IsRarV3 =>
        FileHeader.CompressionAlgorithm == 15
        || FileHeader.CompressionAlgorithm == 20
        || FileHeader.CompressionAlgorithm == 26
        || FileHeader.CompressionAlgorithm == 29
        || FileHeader.CompressionAlgorithm == 36; //Nanook - Added 20+26 as Test arc from WinRar2.8 (algo 20) was failing with 2017 code

    /// <summary>
    /// The File's 32 bit CRC Hash
    /// </summary>
    public override long Crc => BitConverter.ToUInt32(FileHeader.FileCrc, 0);

    /// <summary>
    /// The path of the file internal to the Rar Archive.
    /// </summary>
    public override string? Key => FileHeader.FileName;

    public override string? LinkTarget => null;

    /// <summary>
    /// The entry last modified time in the archive, if recorded
    /// </summary>
    public override DateTime? LastModifiedTime => FileHeader.FileLastModifiedTime;

    /// <summary>
    /// The entry create time in the archive, if recorded
    /// </summary>
    public override DateTime? CreatedTime => FileHeader.FileCreatedTime;

    /// <summary>
    /// The entry last accessed time in the archive, if recorded
    /// </summary>
    public override DateTime? LastAccessedTime => FileHeader.FileLastAccessedTime;

    /// <summary>
    /// The entry time whend archived, if recorded
    /// </summary>
    public override DateTime? ArchivedTime => FileHeader.FileArchivedTime;

    /// <summary>
    /// Entry is password protected and encrypted and cannot be extracted.
    /// </summary>
    public override bool IsEncrypted => FileHeader.IsEncrypted;

    /// <summary>
    /// Entry Windows file attributes
    /// </summary>
    public override int? Attrib => (int)FileHeader.FileAttributes;

    /// <summary>
    /// Entry is a directory
    /// </summary>
    public override bool IsDirectory => FileHeader.IsDirectory;

    public override bool IsSplitAfter => FileHeader.IsSplitAfter;

    public bool IsRedir => FileHeader.IsRedir;

    public string RedirTargetName => FileHeader.RedirTargetName;

    public override string ToString() =>
        string.Format(
            "Entry Path: {0} Compressed Size: {1} Uncompressed Size: {2} CRC: {3}",
            Key,
            CompressedSize,
            Size,
            Crc
        );
}
