using System;
using System.IO;

namespace SharpCompress.Writers.SevenZip;

/// <summary>
/// Provides normalized metadata for deciding how a file should be grouped into a 7z folder.
/// </summary>
public sealed class SevenZipWriteContext
{
    internal SevenZipWriteContext(string entryPath, DateTime? modificationTime)
    {
        EntryPath = entryPath;
        ModificationTime = modificationTime;

        var fileName = Path.GetFileName(entryPath);
        FileName = fileName;
        Extension = Path.GetExtension(fileName).ToLowerInvariant();

        var separatorIndex = entryPath.LastIndexOf('/');
        DirectoryPath = separatorIndex >= 0 ? entryPath.Substring(0, separatorIndex) : string.Empty;
    }

    /// <summary>
    /// Gets the normalized archive entry path.
    /// </summary>
    public string EntryPath { get; }

    /// <summary>
    /// Gets the normalized parent directory path, or an empty string for root entries.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Gets the file name portion of the entry path.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the lowercase file extension, including the leading period when present.
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Gets the file modification time supplied to the writer.
    /// </summary>
    public DateTime? ModificationTime { get; }
}
