using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common;

internal static class ExtractionMethods
{
    /// <summary>
    /// Gets the appropriate StringComparison for path checks based on the file system.
    /// Windows uses case-insensitive file systems, while Unix-like systems use case-sensitive file systems.
    /// </summary>
    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static void WriteEntryToDirectory(
        IEntry entry,
        string destinationDirectory,
        ExtractionOptions? options,
        Action<string, ExtractionOptions?> write
    )
    {
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

        options ??= new ExtractionOptions() { Overwrite = true };

        // Cache entry.Key to avoid multiple property access
        var entryKey = entry.Key.NotNull("Entry Key is null");
        var file = Path.GetFileName(entryKey).NotNull("File is null");
        file = Utility.ReplaceInvalidFileNameChars(file);

        string destinationFileName;
        if (options.ExtractFullPath)
        {
            var folder = Path.GetDirectoryName(entryKey).NotNull("Directory is null");
            // Combine paths first, then get full path once
            destinationFileName = Path.GetFullPath(
                Path.Combine(fullDestinationDirectoryPath, folder, file)
            );

            // Security check before directory creation
            if (!destinationFileName.StartsWith(fullDestinationDirectoryPath, PathComparison))
            {
                throw new ExtractionException(
                    "Entry is trying to write a file outside of the destination directory."
                );
            }

            // Only create parent directory if needed (Directory.CreateDirectory is idempotent but still has overhead)
            var parentDir = Path.GetDirectoryName(destinationFileName)!;
            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
        }
        else
        {
            destinationFileName = Path.GetFullPath(
                Path.Combine(fullDestinationDirectoryPath, file)
            );

            if (!destinationFileName.StartsWith(fullDestinationDirectoryPath, PathComparison))
            {
                throw new ExtractionException(
                    "Entry is trying to write a file outside of the destination directory."
                );
            }
        }

        if (!entry.IsDirectory)
        {
            write(destinationFileName, options);
        }
        else if (options.ExtractFullPath && !Directory.Exists(destinationFileName))
        {
            Directory.CreateDirectory(destinationFileName);
        }
    }

    public static void WriteEntryToFile(
        IEntry entry,
        string destinationFileName,
        ExtractionOptions? options,
        Action<string, FileMode> openAndWrite
    )
    {
        if (entry.LinkTarget != null)
        {
            if (options?.WriteSymbolicLink is null)
            {
                throw new ExtractionException(
                    "Entry is a symbolic link but ExtractionOptions.WriteSymbolicLink delegate is null"
                );
            }
            options.WriteSymbolicLink(destinationFileName, entry.LinkTarget);
        }
        else
        {
            var fm = FileMode.Create;
            options ??= new ExtractionOptions() { Overwrite = true };

            if (!options.Overwrite)
            {
                fm = FileMode.CreateNew;
            }

            openAndWrite(destinationFileName, fm);
            entry.PreserveExtractionOptions(destinationFileName, options);
        }
    }

    public static async ValueTask WriteEntryToDirectoryAsync(
        IEntry entry,
        string destinationDirectory,
        ExtractionOptions? options,
        Func<string, ExtractionOptions?, CancellationToken, ValueTask> writeAsync,
        CancellationToken cancellationToken = default
    )
    {
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

        options ??= new ExtractionOptions() { Overwrite = true };

        // Cache entry.Key to avoid multiple property access
        var entryKey = entry.Key.NotNull("Entry Key is null");
        var file = Path.GetFileName(entryKey).NotNull("File is null");
        file = Utility.ReplaceInvalidFileNameChars(file);

        string destinationFileName;
        if (options.ExtractFullPath)
        {
            var folder = Path.GetDirectoryName(entryKey).NotNull("Directory is null");
            // Combine paths first, then get full path once
            destinationFileName = Path.GetFullPath(
                Path.Combine(fullDestinationDirectoryPath, folder, file)
            );

            // Security check before directory creation
            if (!destinationFileName.StartsWith(fullDestinationDirectoryPath, PathComparison))
            {
                throw new ExtractionException(
                    "Entry is trying to write a file outside of the destination directory."
                );
            }

            // Only create parent directory if needed
            var parentDir = Path.GetDirectoryName(destinationFileName)!;
            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
        }
        else
        {
            destinationFileName = Path.GetFullPath(
                Path.Combine(fullDestinationDirectoryPath, file)
            );

            if (!destinationFileName.StartsWith(fullDestinationDirectoryPath, PathComparison))
            {
                throw new ExtractionException(
                    "Entry is trying to write a file outside of the destination directory."
                );
            }
        }

        if (!entry.IsDirectory)
        {
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
        ExtractionOptions? options,
        Func<string, FileMode, CancellationToken, ValueTask> openAndWriteAsync,
        CancellationToken cancellationToken = default
    )
    {
        if (entry.LinkTarget != null)
        {
            if (options?.WriteSymbolicLink is null)
            {
                throw new ExtractionException(
                    "Entry is a symbolic link but ExtractionOptions.WriteSymbolicLink delegate is null"
                );
            }
            options.WriteSymbolicLink(destinationFileName, entry.LinkTarget);
        }
        else
        {
            var fm = FileMode.Create;
            options ??= new ExtractionOptions() { Overwrite = true };

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
