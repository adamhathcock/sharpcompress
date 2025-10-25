using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers;

public static class IReaderExtensions
{
    public static void WriteEntryTo(this IReader reader, string filePath)
    {
        using Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        reader.WriteEntryTo(stream);
    }

    public static void WriteEntryTo(this IReader reader, FileInfo filePath)
    {
        using Stream stream = filePath.Open(FileMode.Create);
        reader.WriteEntryTo(stream);
    }

    /// <summary>
    /// Extract all remaining unread entries to specific directory, retaining filename
    /// </summary>
    public static void WriteAllToDirectory(
        this IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        while (reader.MoveToNextEntry())
        {
            reader.WriteEntryToDirectory(destinationDirectory, options);
        }
    }

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static void WriteEntryToDirectory(
        this IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) =>
        ExtractionMethods.WriteEntryToDirectory(
            reader.Entry,
            destinationDirectory,
            options,
            reader.WriteEntryToFile
        );

    /// <summary>
    /// Extract to specific file
    /// </summary>
    public static void WriteEntryToFile(
        this IReader reader,
        string destinationFileName,
        ExtractionOptions? options = null
    ) =>
        ExtractionMethods.WriteEntryToFile(
            reader.Entry,
            destinationFileName,
            options,
            (x, fm) =>
            {
                using var fs = File.Open(destinationFileName, fm);
                reader.WriteEntryTo(fs);
            }
        );

    /// <summary>
    /// Extract to specific directory asynchronously, retaining filename
    /// </summary>
    public static async Task WriteEntryToDirectoryAsync(
        this IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        await ExtractionMethods
            .WriteEntryToDirectoryAsync(
                reader.Entry,
                destinationDirectory,
                options,
                (fileName, opts) => reader.WriteEntryToFileAsync(fileName, opts, cancellationToken),
                cancellationToken
            )
            .ConfigureAwait(false);

    /// <summary>
    /// Extract to specific file asynchronously
    /// </summary>
    public static async Task WriteEntryToFileAsync(
        this IReader reader,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        await ExtractionMethods
            .WriteEntryToFileAsync(
                reader.Entry,
                destinationFileName,
                options,
                async (x, fm) =>
                {
                    using var fs = File.Open(destinationFileName, fm);
                    await reader.WriteEntryToAsync(fs, cancellationToken).ConfigureAwait(false);
                },
                cancellationToken
            )
            .ConfigureAwait(false);

    /// <summary>
    /// Extract all remaining unread entries to specific directory asynchronously, retaining filename
    /// </summary>
    public static async Task WriteAllToDirectoryAsync(
        this IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        while (reader.MoveToNextEntry())
        {
            await reader
                .WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
