using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers;

public static class IAsyncReaderExtensions
{
    extension(IAsyncReader reader)
    {
        /// <summary>
        /// Extract to specific directory asynchronously, retaining filename
        /// </summary>
        public async ValueTask WriteEntryToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await ExtractionMethods
                .WriteEntryToDirectoryAsync(
                    reader.Entry,
                    destinationDirectory,
                    options,
                    reader.WriteEntryToFileAsync,
                    cancellationToken
                )
                .ConfigureAwait(false);

        /// <summary>
        /// Extract to specific file asynchronously
        /// </summary>
        public async ValueTask WriteEntryToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await ExtractionMethods
                .WriteEntryToFileAsync(
                    reader.Entry,
                    destinationFileName,
                    options,
                    async (x, fm, ct) =>
                    {
                        using var fs = File.Open(destinationFileName, fm);
                        await reader.WriteEntryToAsync(fs, ct).ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

        /// <summary>
        /// Extract all remaining unread entries to specific directory asynchronously, retaining filename
        /// </summary>
        public async ValueTask WriteAllToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            while (await reader.MoveToNextEntryAsync(cancellationToken))
            {
                await reader
                    .WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask WriteEntryToAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await ExtractionMethods
                .WriteEntryToFileAsync(
                    reader.Entry,
                    destinationFileName,
                    options,
                    async (x, fm, ct) =>
                    {
                        using var fs = File.Open(destinationFileName, fm);
                        await reader.WriteEntryToAsync(fs, ct).ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

        public async ValueTask WriteEntryToAsync(
            FileInfo destinationFileInfo,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await reader
                .WriteEntryToAsync(destinationFileInfo.FullName, options, cancellationToken)
                .ConfigureAwait(false);
    }
}
