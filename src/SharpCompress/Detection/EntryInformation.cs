using System;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Detection;

/// <summary>A detached metadata snapshot. Null sizes mean unavailable, not zero.</summary>
public sealed class EntryInformation
{
    private EntryInformation() { }

    public string? Key { get; private set; }
    public string? LinkTarget { get; private set; }
    public CompressionType CompressionType { get; private set; }
    public long? Size { get; private set; }
    public long? CompressedSize { get; private set; }
    public bool HasDeferredSizes { get; private set; }
    public bool IsDirectory { get; private set; }
    public bool IsEncrypted { get; private set; }
    public bool IsSolid { get; private set; }
    public bool IsSplitAfter { get; private set; }
    public bool? IsComplete { get; private set; }
    public int VolumeIndexFirst { get; private set; }
    public int VolumeIndexLast { get; private set; }
    public DateTime? LastModifiedTime { get; private set; }
    public DateTime? CreatedTime { get; private set; }
    public DateTime? LastAccessedTime { get; private set; }
    public DateTime? ArchivedTime { get; private set; }
    public int? Attributes { get; private set; }

    /// <summary>Read metadata without opening the payload or interpreting placeholder sizes as real values.</summary>
    public static EntryInformation FromEntry(IEntry entry)
    {
        ThrowHelper.ThrowIfNull(entry);
        var deferred = false;
        if (entry is ZipEntry zip)
        {
            foreach (var part in zip.Parts)
            {
                if (
                    part is ZipFilePart file
                    && FlagUtility.HasFlag(file.Header.Flags, HeaderFlags.UsePostDataDescriptor)
                    && !file.Header.IsCrcAvailable
                    && file.Header.CompressedSize == 0
                    && file.Header.UncompressedSize == 0
                )
                {
                    deferred = true;
                    break;
                }
            }
        }
        // Standalone GZip does not expose reliable sizes. A compressed TAR still
        // has uncompressed sizes in its headers, but no per-entry compressed sizes.
        var unknown = deferred || entry is GZipEntry;
        var compressedTar = entry is TarEntry && entry.CompressionType != CompressionType.None;
        return new EntryInformation
        {
            Key = entry.Key,
            LinkTarget = entry.LinkTarget,
            CompressionType = entry.CompressionType,
            Size = unknown ? null : Available(() => entry.Size),
            CompressedSize =
                unknown
                || entry.IsSolid
                || compressedTar
                || entry is SharpCompress.Archives.SevenZip.SevenZipArchiveEntry
                    ? null
                    : Available(() => entry.CompressedSize),
            HasDeferredSizes = deferred,
            IsDirectory = entry.IsDirectory,
            IsEncrypted = entry.IsEncrypted,
            IsSolid = entry.IsSolid,
            IsSplitAfter = entry.IsSplitAfter,
            IsComplete = (entry as IArchiveEntry)?.IsComplete,
            VolumeIndexFirst = entry.VolumeIndexFirst,
            VolumeIndexLast = entry.VolumeIndexLast,
            LastModifiedTime = entry.LastModifiedTime,
            CreatedTime = entry.CreatedTime,
            LastAccessedTime = entry.LastAccessedTime,
            ArchivedTime = entry.ArchivedTime,
            Attributes = Attribute(entry),
        };
    }

    private static long? Available(Func<long> read)
    {
        try
        {
            var value = read();
            return value >= 0 ? value : null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private static int? Attribute(IEntry entry)
    {
        try
        {
            return entry.Attrib;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }
}
