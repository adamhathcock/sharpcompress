using System.IO;

namespace SharpCompress.Archives.Extraction;

internal sealed class ArchiveConcurrencyInfo
{
    internal ArchiveConcurrencyInfo(ArchiveConcurrencyMode mode, FileInfo? sourceFile)
    {
        Mode = mode;
        SourceFile = sourceFile;
    }

    internal ArchiveConcurrencyMode Mode { get; }

    internal FileInfo? SourceFile { get; }
}
