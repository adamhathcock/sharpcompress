using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archive
{
    public static class IArchiveEntryExtensions
    {
        public static void WriteTo(this IArchiveEntry archiveEntry, Stream streamToWriteTo)
        {
            if (archiveEntry.Archive.Type == ArchiveType.Rar && archiveEntry.Archive.IsSolid)
            {
                throw new InvalidFormatException("Cannot use Archive random access on SOLID Rar files.");
            }

            if (archiveEntry.IsDirectory)
            {
                throw new ExtractionException("Entry is a file directory and cannot be extracted.");
            }

            var streamListener = archiveEntry.Archive as IArchiveExtractionListener;
            streamListener.EnsureEntriesLoaded();
            streamListener.FireEntryExtractionBegin(archiveEntry);
            streamListener.FireFilePartExtractionBegin(archiveEntry.FilePath, archiveEntry.Size, archiveEntry.CompressedSize);
            using (Stream s = new ListeningStream(streamListener, archiveEntry.OpenEntryStream()))
            {
                s.TransferTo(streamToWriteTo);
            }
            streamListener.FireEntryExtractionEnd(archiveEntry);
        }

#if !PORTABLE && !NETFX_CORE
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteToDirectory(this IArchiveEntry entry, string destinationDirectory,
                                            ExtractOptions options = ExtractOptions.Overwrite)
        {
            string destinationFileName;
            string file = Path.GetFileName(entry.FilePath);


            if (options.HasFlag(ExtractOptions.ExtractFullPath))
            {
                string folder = Path.GetDirectoryName(entry.FilePath);
                string destdir = Path.Combine(destinationDirectory, folder);
                if (!Directory.Exists(destdir))
                {
                    Directory.CreateDirectory(destdir);
                }
                destinationFileName = Path.Combine(destdir, file);
            }
            else
            {
                destinationFileName = Path.Combine(destinationDirectory, file);
            }
            entry.WriteToFile(destinationFileName, options);
        }

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public static void WriteToFile(this IArchiveEntry entry, string destinationFileName,
                                       ExtractOptions options = ExtractOptions.Overwrite)
        {
            if (entry.IsDirectory)
            {
                return;
            }
            FileMode fm = FileMode.Create;

            if (!options.HasFlag(ExtractOptions.Overwrite))
            {
                fm = FileMode.CreateNew;
            }
            using (FileStream fs = File.Open(destinationFileName, fm))
            {
                entry.WriteTo(fs);
            }
        }
#endif
    }
}