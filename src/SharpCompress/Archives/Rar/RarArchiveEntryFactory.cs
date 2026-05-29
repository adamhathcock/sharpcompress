using System.Collections.Generic;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar;

internal static class RarArchiveEntryFactory
{
    private static IEnumerable<RarFilePart> GetFileParts(IEnumerable<RarVolume> parts)
    {
        foreach (var rarPart in parts)
        {
            foreach (var fp in rarPart.ReadFileParts())
            {
                yield return fp;
            }
        }
    }

    private static async IAsyncEnumerable<RarFilePart> GetFilePartsAsync(
        IAsyncEnumerable<RarVolume> parts
    )
    {
        await foreach (var rarPart in parts.ConfigureAwait(false))
        {
            await foreach (var fp in rarPart.ReadFilePartsAsync().ConfigureAwait(false))
            {
                yield return fp;
            }
        }
    }

    private static IEnumerable<IEnumerable<RarFilePart>> GetMatchedFileParts(
        IEnumerable<RarVolume> parts
    )
    {
        var groupedParts = new List<RarFilePart>();
        foreach (var fp in GetFileParts(parts))
        {
            groupedParts.Add(fp);

            if (!fp.FileHeader.IsSplitAfter)
            {
                yield return groupedParts;
                groupedParts = new List<RarFilePart>();
            }
        }
        if (groupedParts.Count > 0)
        {
            yield return groupedParts;
        }
    }

    private static async IAsyncEnumerable<IEnumerable<RarFilePart>> GetMatchedFilePartsAsync(
        IAsyncEnumerable<RarVolume> parts
    )
    {
        var groupedParts = new List<RarFilePart>();
        await foreach (var fp in GetFilePartsAsync(parts).ConfigureAwait(false))
        {
            groupedParts.Add(fp);

            if (!fp.FileHeader.IsSplitAfter)
            {
                yield return groupedParts;
                groupedParts = new List<RarFilePart>();
            }
        }
        if (groupedParts.Count > 0)
        {
            yield return groupedParts;
        }
    }

    internal static IEnumerable<RarArchiveEntry> GetEntries(
        RarArchive archive,
        IEnumerable<RarVolume> rarParts,
        ReaderOptions readerOptions
    )
    {
        foreach (var groupedParts in GetMatchedFileParts(rarParts))
        {
            yield return new RarArchiveEntry(archive, groupedParts, readerOptions);
        }
    }

    internal static async IAsyncEnumerable<RarArchiveEntry> GetEntriesAsync(
        RarArchive archive,
        IAsyncEnumerable<RarVolume> rarParts,
        ReaderOptions readerOptions
    )
    {
        await foreach (var groupedParts in GetMatchedFilePartsAsync(rarParts).ConfigureAwait(false))
        {
            yield return new RarArchiveEntry(archive, groupedParts, readerOptions);
        }
    }
}
