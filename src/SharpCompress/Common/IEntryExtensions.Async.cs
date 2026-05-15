using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common;

internal static partial class IEntryExtensions
{
    extension(IEntry entry)
    {
        internal async ValueTask WriteEntryToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options,
            Func<string, CancellationToken, ValueTask> writeAsync,
            CancellationToken cancellationToken = default
        )
        {
            options ??= new ExtractionOptions();
            var fullDestinationDirectoryPath = DirectoryManagement.GetFullDestinationDirectoryPath(
                destinationDirectory
            );

            await WriteEntryToDirectoryAsyncCore(
                    entry,
                    fullDestinationDirectoryPath,
                    options,
                    writeAsync,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        internal async ValueTask WriteEntryToDirectoryAsyncCore(
            string fullDestinationDirectoryPath,
            ExtractionOptions options,
            Func<string, CancellationToken, ValueTask>? writeAsync,
            CancellationToken cancellationToken = default
        )
        {
            var destinationFileName = GetEntryDestinationFileName(
                entry,
                fullDestinationDirectoryPath,
                options
            );

            if (!entry.IsDirectory)
            {
                destinationFileName = Path.GetFullPath(destinationFileName);

                DirectoryManagement.EnsurePathInDestinationDirectory(
                    destinationFileName,
                    fullDestinationDirectoryPath,
                    DirectoryManagement.WriteFileOutsideDestinationMessage
                );

                if (writeAsync != null)
                {
                    await writeAsync(destinationFileName, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (options.ExtractFullPath)
            {
                destinationFileName = Path.GetFullPath(destinationFileName);

                DirectoryManagement.EnsurePathInDestinationDirectory(
                    destinationFileName,
                    fullDestinationDirectoryPath,
                    DirectoryManagement.CreateDirectoryOutsideDestinationMessage
                );

                if (!Directory.Exists(destinationFileName))
                {
                    Directory.CreateDirectory(destinationFileName);
                }
            }
        }

        public async ValueTask WriteEntryToFileAsync(
            string destinationFileName,
            ExtractionOptions? options,
            Func<string, FileMode, CancellationToken, ValueTask> openAndWriteAsync,
            CancellationToken cancellationToken = default
        )
        {
            options ??= new ExtractionOptions();
            if (entry.LinkTarget != null)
            {
                options.SymbolicLinkHandler?.Invoke(destinationFileName, entry.LinkTarget);
            }
            else
            {
                var fm = FileMode.Create;

                if (!options.Overwrite)
                {
                    fm = FileMode.CreateNew;
                }

                await openAndWriteAsync(destinationFileName, fm, cancellationToken)
                    .ConfigureAwait(false);
                entry.PreserveExtractionOptions(destinationFileName, options);
            }
        }
    }
}
