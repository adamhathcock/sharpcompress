using System.Collections.Generic;
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
        groupedParts = [];
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
}
