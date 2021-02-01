using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common
{
    internal static class ExtractionMethods
    {
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static async ValueTask WriteEntryToDirectoryAsync(IEntry entry,
                                                 string destinationDirectory,
                                                 ExtractionOptions? options,
                                                 Func<string, ExtractionOptions?, CancellationToken, ValueTask> write,
                                                 CancellationToken cancellationToken = default)
        {
            string destinationFileName;
            string file = Path.GetFileName(entry.Key);
            string fullDestinationDirectoryPath = Path.GetFullPath(destinationDirectory);

            options ??= new ExtractionOptions()
            {
                Overwrite = true
            };

            if (options.ExtractFullPath)
            {
                string folder = Path.GetDirectoryName(entry.Key)!;
                string destdir = Path.GetFullPath(Path.Combine(fullDestinationDirectoryPath, folder));

                if (!Directory.Exists(destdir))
                {
                    if (!destdir.StartsWith(fullDestinationDirectoryPath, StringComparison.Ordinal))
                    {
                        throw new ExtractionException("Entry is trying to create a directory outside of the destination directory.");
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

                if (!destinationFileName.StartsWith(fullDestinationDirectoryPath, StringComparison.Ordinal))
                {
                    throw new ExtractionException("Entry is trying to write a file outside of the destination directory.");
                }
                await write(destinationFileName, options, cancellationToken);
            }
            else if (options.ExtractFullPath && !Directory.Exists(destinationFileName))
            {
                Directory.CreateDirectory(destinationFileName);
            }
        }

        public static async ValueTask WriteEntryToFileAsync(IEntry entry, string destinationFileName,
                                            ExtractionOptions? options,
                                            Func<string, FileMode, CancellationToken, ValueTask> openAndWrite,
                                            CancellationToken cancellationToken = default)
        {
            if (entry.LinkTarget is not null)
            {
                if (options?.WriteSymbolicLink is null)
                {
                    throw new ExtractionException("Entry is a symbolic link but ExtractionOptions.WriteSymbolicLink delegate is null");
                }
                options.WriteSymbolicLink(destinationFileName, entry.LinkTarget);
            }
            else
            {
                FileMode fm = FileMode.Create;
                options ??= new ExtractionOptions()
                {
                    Overwrite = true
                };

                if (!options.Overwrite)
                {
                    fm = FileMode.CreateNew;
                }

                await openAndWrite(destinationFileName, fm, cancellationToken);
                entry.PreserveExtractionOptions(destinationFileName, options);
            }
        }
    }
}