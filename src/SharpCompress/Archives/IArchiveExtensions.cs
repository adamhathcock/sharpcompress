using System;
using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>Convenience extraction using the default stateless extraction service.</summary>
public static class IArchiveExtensions
{
    extension(IArchive archive)
    {
        public void WriteToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null
        ) =>
            ClientDefaults.Client.Extractor.ExtractToDirectory(
                archive,
                destinationDirectory,
                options,
                progress
            );
    }
}
