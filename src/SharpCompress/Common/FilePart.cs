using System.IO;

namespace SharpCompress.Common;

public abstract class FilePart
{
    protected FilePart(IArchiveEncoding archiveEncoding) => ArchiveEncoding = archiveEncoding;

    internal IArchiveEncoding ArchiveEncoding { get; }

    internal abstract string? FilePartName { get; }
    public int Index { get; set; }

    internal abstract Stream? GetCompressedStream();
    internal abstract Stream? GetRawStream();
    internal bool Skipped { get; set; }
}
