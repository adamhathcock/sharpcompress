using System;
using System.IO;

namespace SharpCompress.Common;

internal static partial class IEntryExtensions
{
    extension(IEntry entry)
    {
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public void WriteEntryToDirectory(
            string destinationDirectory,
            ExtractionOptions? options,
            Action<string> write
        )
        {
            options ??= new ExtractionOptions();
            var fullDestinationDirectoryPath = DirectoryManagement.GetFullDestinationDirectoryPath(
                destinationDirectory
            );

            WriteEntryToDirectoryCore(entry, fullDestinationDirectoryPath, options, write);
        }

        internal void WriteEntryToDirectoryCore(
            string fullDestinationDirectoryPath,
            ExtractionOptions options,
            Action<string>? write
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
            write?.Invoke(destinationFileName);
        }

        private string GetEntryDestinationFileName(
            string fullDestinationDirectoryPath,
            ExtractionOptions options
        )
        {
            var file = Path.GetFileName(entry.Key.NotNull("Entry Key is null"))
                .NotNull("File is null");
            file = Utility.ReplaceInvalidFileNameChars(file);

            if (options.ExtractFullPath)
            {
                var folder = Path.GetDirectoryName(entry.Key.NotNull("Entry Key is null"))
                    .NotNull("Directory is null");
                var destdir = Path.GetFullPath(Path.Combine(fullDestinationDirectoryPath, folder));

                DirectoryManagement.EnsurePathInDestinationDirectory(
                    destdir,
                    fullDestinationDirectoryPath,
                    entry.IsDirectory
                        ? DirectoryManagement.CreateDirectoryOutsideDestinationMessage
                        : DirectoryManagement.WriteFileOutsideDestinationMessage
                );

                if (!Directory.Exists(destdir))
                {
                    Directory.CreateDirectory(destdir);
                }

                return Path.Combine(destdir, file);
            }

            return Path.Combine(fullDestinationDirectoryPath, file);
        }

        public void WriteEntryToFile(
            string destinationFileName,
            ExtractionOptions? options,
            Action<string, FileMode> openAndWrite
        )
        {
            options ??= new ExtractionOptions();
            if (entry.LinkTarget != null)
            {
                if (options.SymbolicLinkHandler is not null)
                {
                    options.SymbolicLinkHandler(destinationFileName, entry.LinkTarget);
                }
                else
                {
                    ExtractionOptions.DefaultSymbolicLinkHandler(
                        destinationFileName,
                        entry.LinkTarget
                    );
                }

                return;
            }
            else
            {
                var fm = FileMode.Create;

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
