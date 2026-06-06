using System.Collections.Generic;
using System.Linq;

namespace SharpCompress.Archives.Extraction;

internal sealed class ArchiveExtractionGroup
{
    internal ArchiveExtractionGroup(IEnumerable<string> entryKeys, bool isSolid)
    {
        EntryKeys = entryKeys.ToArray();
        IsSolid = isSolid;
    }

    internal IReadOnlyList<string> EntryKeys { get; }

    internal bool IsSolid { get; }
}
