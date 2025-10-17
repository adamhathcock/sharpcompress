using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers;

public static class IReaderExtensions
{
    public static async Task  WriteEntryToAsync(this IReader reader, string filePath)
    {
        using Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        await reader.WriteEntryToAsync(stream);
    }

    public static async Task WriteEntryToAsync(this IReader reader, FileInfo filePath)
    {
        using Stream stream = filePath.Open(FileMode.Create);
        await reader.WriteEntryToAsync(stream);
    }

    /// <summary>
    /// Extract all remaining unread entries to specific directory, retaining filename
    /// </summary>
    public static async Task WriteAllToDirectoryAsync(
        this IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        while (await reader.MoveToNextEntryAsync())
        {
            await reader.WriteEntryToDirectoryAsync(destinationDirectory, options);
        }
    }

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static async Task WriteEntryToDirectoryAsync(
        this IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) =>
        await ExtractionMethods.WriteEntryToDirectoryAsync(
            reader.Entry,
            destinationDirectory,
            options,
            reader.WriteEntryToFileAsync
        );

    /// <summary>
    /// Extract to specific file
    /// </summary>
    public static async Task  WriteEntryToFileAsync(
        this IReader reader,
        string destinationFileName,
        ExtractionOptions? options = null
    ) =>
        await ExtractionMethods.WriteEntryToFileAsync(
            reader.Entry,
            destinationFileName,
            options,
            async (x, fm) =>
            {
                using var fs = File.Open(destinationFileName, fm);
                await reader.WriteEntryToAsync(fs);
            }
        );
}
