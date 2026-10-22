using System;
using SharpCompress.Common;
using SharpCompress.Services;

namespace SharpCompress.Archives;

public static class IArchiveExtensions
{
    extension(IArchive archive)
    {
        /// <summary>Extract to specific directory with progress reporting.</summary>
        /// <param name="destinationDirectory">The folder to extract into.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        public void WriteToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null
        ) =>
            LibraryDefaults.Extractor.WriteToDirectory(
                archive,
                destinationDirectory,
                options,
                progress
            );
    }
}
