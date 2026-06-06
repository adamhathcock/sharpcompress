using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Extraction;
using SharpCompress.Common;

namespace SharpCompress.Archives;

public static class IAsyncArchiveExtensions
{
    extension(IAsyncArchive archive)
    {
        /// <summary>
        /// Extract to specific directory asynchronously with progress reporting and cancellation support
        /// </summary>
        /// <param name="destinationDirectory">The folder to extract into.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        ) =>
            await new ArchiveExtractionCoordinator(
                archive,
                destinationDirectory,
                options,
                progress,
                cancellationToken
            )
                .WriteToDirectoryAsync()
                .ConfigureAwait(false);
    }
}
