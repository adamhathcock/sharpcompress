using SharpCompress.Common;

namespace SharpCompress.Archives;

public interface IArchiveEntry : IEntry
{
    /// <summary>
    /// The archive can find all the parts of the archive needed to extract this entry.
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// The archive instance this entry belongs to
    /// </summary>
    IArchive Archive { get; }
}
