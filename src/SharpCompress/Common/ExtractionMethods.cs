using System;
using System.IO;

namespace SharpCompress.Common
{
    internal static class ExtractionMethods
    {
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteEntryToDirectory(IEntry entry,
                                                 string destinationDirectory,
                                                 ExtractionOptions? options,
                                                 Action<string, ExtractionOptions?> write)
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
                write(destinationFileName, options);
            }
            else if (options.ExtractFullPath && !Directory.Exists(destinationFileName))
            {
                Directory.CreateDirectory(destinationFileName);
            }
        }

        public static void WriteEntryToFile(IEntry entry, string destinationFileName,
                                            ExtractionOptions? options,
                                            Action<string, FileMode> openAndWrite)
        {
            if (entry.LinkTarget != null)
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

                openAndWrite(destinationFileName, fm);
                entry.PreserveExtractionOptions(destinationFileName, options);
            }
        }
    }
}