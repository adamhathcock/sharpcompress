using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Extraction;

internal sealed record ArchiveExtractionConcurrencyInfo
{
    internal ArchiveExtractionConcurrencyInfo(ArchiveType type) => Type = type;

    internal ArchiveType Type { get; }

    internal ArchiveConcurrencyMode Mode { get; init; } = ArchiveConcurrencyMode.SequentialOnly;

    internal bool SupportsIndependentEntryStreams { get; init; }

    internal bool SupportsIndependentSolidStreams { get; init; }

    internal bool RequiresSeekableStream { get; init; }

    internal FileInfo? SourceFile { get; init; }

    internal ReaderOptions? ReaderOptions { get; init; }

    internal IReadOnlyList<ArchiveExtractionGroup> Groups { get; init; } =
        Array.Empty<ArchiveExtractionGroup>();
}
