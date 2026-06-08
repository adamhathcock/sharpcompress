using System;

namespace SharpCompress.Archives.Extraction;

internal sealed class ArchiveExtractionException : Exception
{
    internal ArchiveExtractionException(string message, string? entryKey, Exception innerException)
        : base(message, innerException) => EntryKey = entryKey;

    internal string? EntryKey { get; }
}
