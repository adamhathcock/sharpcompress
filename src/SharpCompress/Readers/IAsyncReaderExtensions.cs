using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers;

/// <summary>Convenience extraction using the default extraction service.</summary>
public static class IAsyncReaderExtensions
{
    extension(IAsyncReader reader)
    {
        public ValueTask WriteEntryToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractEntryToDirectoryAsync(
                reader,
                destinationDirectory,
                options,
                cancellationToken
            );

        public ValueTask WriteEntryToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToFileAsync(
                reader,
                destinationFileName,
                options,
                cancellationToken
            );

        public ValueTask WriteAllToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToDirectoryAsync(
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
            ClientDefaults.Client.Extractor.ExtractToFileAsync(
                reader,
                destinationFileName,
                options,
                cancellationToken
            );

        public ValueTask WriteEntryToAsync(
            FileInfo destinationFileInfo,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToFileAsync(
                reader,
                destinationFileInfo.FullName,
                options,
                cancellationToken
            );
    }
}
