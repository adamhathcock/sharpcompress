using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Services;

// Pure stream decoration shared by entry and reader workflows; it never takes stream ownership.
internal static class ExtractionStreamHelper
{
    public static Stream WrapWithProgress(
        Stream source,
        IEntry entry,
        IProgress<ProgressReport>? progress
    )
    {
        if (progress is null)
        {
            return source;
        }
        return new ProgressReportingStream(
            source,
            progress,
            entry.Key ?? string.Empty,
            GetEntrySizeSafe(entry),
            leaveOpen: true
        );
    }

    private static long? GetEntrySizeSafe(IEntry entry)
    {
        try
        {
            var size = entry.Size;
            return size >= 0 ? size : null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }
}
