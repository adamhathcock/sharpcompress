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
        internal void WriteEntryToDirectory(
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
                write?.Invoke(destinationFileName);
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
                options.SymbolicLinkHandler?.Invoke(destinationFileName, entry.LinkTarget);
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

        internal void PreserveExtractionOptions(
            string destinationFileName,
            ExtractionOptions options
        )
        {
            if (options.PreserveFileTime || options.PreserveAttributes)
            {
                var nf = new FileInfo(destinationFileName);
                if (!nf.Exists)
                {
                    return;
                }

                // update file time to original packed time
                if (options.PreserveFileTime)
                {
                    if (entry.CreatedTime.HasValue)
                    {
                        try
                        {
                            nf.CreationTime = entry.CreatedTime.Value;
                        }
                        catch
                        {
                            // Invalid time or the OS rejected
                        }
                    }

                    if (entry.LastModifiedTime.HasValue)
                    {
                        try
                        {
                            nf.LastWriteTime = entry.LastModifiedTime.Value;
                        }
                        catch
                        {
                            // Invalid time or the OS rejected
                        }
                    }

                    if (entry.LastAccessedTime.HasValue)
                    {
                        try
                        {
                            nf.LastAccessTime = entry.LastAccessedTime.Value;
                        }
                        catch
                        {
                            // Invalid time or the OS rejected
                        }
                    }
                }

                if (options.PreserveAttributes)
                {
                    if (entry.Attrib.HasValue)
                    {
                        nf.Attributes = (FileAttributes)
                            Enum.ToObject(typeof(FileAttributes), entry.Attrib.Value);
                    }
                }
            }
        }
    }
}
