using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Services;

namespace SharpCompress.Readers;

public static class IAsyncReaderExtensions
{
    extension(IAsyncReader reader)
    {
        /// <summary>Extract to specific directory asynchronously, retaining filename.</summary>
        public ValueTask WriteEntryToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryToDirectoryAsync(
                reader,
                destinationDirectory,
                options,
                cancellationToken
            );

        /// <summary>Extract to specific file asynchronously.</summary>
        public ValueTask WriteEntryToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryToFileAsync(
                reader,
                destinationFileName,
                options,
                cancellationToken
            );

        /// <summary>Extract all remaining unread entries to specific directory asynchronously, retaining filename.</summary>
        public ValueTask WriteAllToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteAllToDirectoryAsync(
                reader,
                destinationDirectory,
                options,
                cancellationToken
            );

        public ValueTask WriteEntryToAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryToFileAsync(
                reader,
                destinationFileName,
                options,
                cancellationToken
            );

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
