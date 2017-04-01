using System.Collections.Generic;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Archive.Rar
{
    internal static class RarArchiveEntryFactory
    {
        private static IEnumerable<RarFilePart> GetFileParts(IEnumerable<RarVolume> parts)
        {
            foreach (RarVolume rarPart in parts)
            {
                foreach (RarFilePart fp in rarPart.ReadFileParts())
                {
                    yield return fp;
                }
            }
        }

        private static IEnumerable<IEnumerable<RarFilePart>> GetMatchedFileParts(IEnumerable<RarVolume> parts)
        {
            var groupedParts = new List<RarFilePart>();
            foreach (RarFilePart fp in GetFileParts(parts))
            {
                groupedParts.Add(fp);

                if (!FlagUtility.HasFlag((long)fp.FileHeader.FileFlags, (long)FileFlags.SPLIT_AFTER))
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

        internal static IEnumerable<RarArchiveEntry> GetEntries(RarArchive archive,
                                                                IEnumerable<RarVolume> rarParts)
        {
            foreach (var groupedParts in GetMatchedFileParts(rarParts))
            {
                yield return new RarArchiveEntry(archive, groupedParts);
            }
        }
    }
}
