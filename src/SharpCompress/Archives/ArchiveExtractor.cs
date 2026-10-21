using SharpCompress.Common;
using SharpCompress.Services;

namespace SharpCompress.Archives;

/// <summary>Extract archives to the filesystem.</summary>
public static class ArchiveExtractor
{
    /// <summary>Extract the archive at the specified path to a directory.</summary>
    public static void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) => LibraryDefaults.Extractor.WriteToDirectory(sourceArchive, destinationDirectory, options);
}
