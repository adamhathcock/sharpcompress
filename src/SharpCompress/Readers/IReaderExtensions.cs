using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers
{
    public static class IReaderExtensions
    {
        public static async ValueTask WriteEntryToAsync(this IReader reader, string filePath)
        {
            await using Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
            await reader.WriteEntryToAsync(stream);
        }

        public static async ValueTask WriteEntryToAsync(this IReader reader, FileInfo filePath)
        {
            await using Stream stream = filePath.Open(FileMode.Create);
            await reader.WriteEntryToAsync(stream);
        }

        /// <summary>
        /// Extract all remaining unread entries to specific directory, retaining filename
        /// </summary>
        public static async ValueTask WriteAllToDirectoryAsync(this IReader reader, string destinationDirectory,
                                               ExtractionOptions? options = null)
        {
            while (await reader.MoveToNextEntryAsync())
            {
               await reader.WriteEntryToDirectoryAsync(destinationDirectory, options);
            }
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static ValueTask WriteEntryToDirectoryAsync(this IReader reader, string destinationDirectory,
                                                      ExtractionOptions? options = null,
                                                      CancellationToken cancellationToken = default)
        {
            if (reader.Entry is null)
            {
                throw new ArgumentException("Entry is null");
            }
            return ExtractionMethods.WriteEntryToDirectoryAsync(reader.Entry, destinationDirectory, options,
                                                                async (x, o, ct) =>
                                                                {
                                                                    await reader.WriteEntryToFileAsync(x, o, ct);
                                                                }, cancellationToken);
        }

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public static async ValueTask WriteEntryToFileAsync(this IReader reader,
                                            string destinationFileName,
                                            ExtractionOptions? options = null,
                                            CancellationToken cancellationToken = default)
        {
            if (reader.Entry is null)
            {
                throw new ArgumentException("Entry is null");
            }
            await ExtractionMethods.WriteEntryToFileAsync(reader.Entry, destinationFileName, options,
                                               async (x, fm, ct) =>
                                               {
                                                   await using FileStream fs = File.Open(x, fm);
                                                   await reader.WriteEntryToAsync(fs, ct);
                                               }, cancellationToken);
        }
    }
}