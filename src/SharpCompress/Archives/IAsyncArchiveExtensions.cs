using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>Convenience extraction using the default stateless extraction service.</summary>
public static class IAsyncArchiveExtensions
{
    extension(IAsyncArchive archive)
    {
        public ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToDirectoryAsync(
                archive,
                destinationDirectory,
                options,
                progress,
                cancellationToken
            );
    }
}
