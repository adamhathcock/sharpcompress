using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Readers;

namespace SharpCompress.Common;

internal static partial class ExtractionMethods
{
    public static async ValueTask WriteEntryToDirectoryAsync(
        IEntry entry,
        string destinationDirectory,
        ReaderOptions? options,
        Func<string, ReaderOptions?, CancellationToken, ValueTask> writeAsync,
        CancellationToken cancellationToken = default
    )
    {
        string destinationFileName;
        var fullDestinationDirectoryPath = Path.GetFullPath(destinationDirectory);

        //check for trailing slash.
        if (
            fullDestinationDirectoryPath[fullDestinationDirectoryPath.Length - 1]
            != Path.DirectorySeparatorChar
        )
        {
            fullDestinationDirectoryPath += Path.DirectorySeparatorChar;
        }

        if (!Directory.Exists(fullDestinationDirectoryPath))
        {
            throw new ExtractionException(
                $"Directory does not exist to extract to: {fullDestinationDirectoryPath}"
            );
        }

        options ??= new ReaderOptions { Overwrite = true };

        var file = Path.GetFileName(entry.Key.NotNull("Entry Key is null")).NotNull("File is null");
        file = Utility.ReplaceInvalidFileNameChars(file);
        if (options.ExtractFullPath)
        {
            var folder = Path.GetDirectoryName(entry.Key.NotNull("Entry Key is null"))
                .NotNull("Directory is null");
            var destdir = Path.GetFullPath(Path.Combine(fullDestinationDirectoryPath, folder));

            if (!Directory.Exists(destdir))
            {
                if (!destdir.StartsWith(fullDestinationDirectoryPath, PathComparison))
                {
                    throw new ExtractionException(
                        "Entry is trying to create a directory outside of the destination directory."
                    );
                }

                Directory.CreateDirectory(destdir);
            }
            destinationFileName = Path.Combine(destdir, file);
        }
        else
        {
            destinationFileName = Path.Combine(fullDestinationDirectoryPath, file);
        }

        if (!entry.IsDirectory)
        {
            destinationFileName = Path.GetFullPath(destinationFileName);

            if (!destinationFileName.StartsWith(fullDestinationDirectoryPath, PathComparison))
            {
                throw new ExtractionException(
                    "Entry is trying to write a file outside of the destination directory."
                );
            }
            await writeAsync(destinationFileName, options, cancellationToken).ConfigureAwait(false);
        }
        else if (options.ExtractFullPath && !Directory.Exists(destinationFileName))
        {
            Directory.CreateDirectory(destinationFileName);
        }
    }

    public static async ValueTask WriteEntryToFileAsync(
        IEntry entry,
        string destinationFileName,
        ReaderOptions? options,
        Func<string, FileMode, CancellationToken, ValueTask> openAndWriteAsync,
        CancellationToken cancellationToken = default
    )
    {
        if (entry.LinkTarget != null)
        {
            if (options?.SymbolicLinkHandler is not null)
            {
                options.SymbolicLinkHandler(destinationFileName, entry.LinkTarget);
            }
            else
            {
                ReaderOptions.DefaultSymbolicLinkHandler(destinationFileName, entry.LinkTarget);
            }
            return;
        }
        else
        {
            var fm = FileMode.Create;
            options ??= new ReaderOptions { Overwrite = true };

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
