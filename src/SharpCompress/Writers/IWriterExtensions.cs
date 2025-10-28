using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Writers;

public static class IWriterExtensions
{
    public static void Write(this IWriter writer, string entryPath, Stream source) =>
        writer.Write(entryPath, source, null);

    public static void Write(this IWriter writer, string entryPath, FileInfo source)
    {
        if (!source.Exists)
        {
            throw new ArgumentException("Source does not exist: " + source.FullName);
        }
        using var stream = source.OpenRead();
        writer.Write(entryPath, stream, source.LastWriteTime);
    }

    public static void Write(this IWriter writer, string entryPath, string source) =>
        writer.Write(entryPath, new FileInfo(source));

    public static void WriteAll(
        this IWriter writer,
        string directory,
        string searchPattern = "*",
        SearchOption option = SearchOption.TopDirectoryOnly
    ) => writer.WriteAll(directory, searchPattern, null, option);

    public static void WriteAll(
        this IWriter writer,
        string directory,
        string searchPattern = "*",
        Func<string, bool>? fileSearchFunc = null,
        SearchOption option = SearchOption.TopDirectoryOnly
    )
    {
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException("Directory does not exist: " + directory);
        }

        fileSearchFunc ??= n => true;
        foreach (
            var file in Directory
                .EnumerateFiles(directory, searchPattern, option)
                .Where(fileSearchFunc)
        )
        {
            writer.Write(file.Substring(directory.Length), file);
        }
    }

    public static void WriteDirectory(this IWriter writer, string directoryName) =>
        writer.WriteDirectory(directoryName, null);

    // Async extensions
    public static Task WriteAsync(
        this IWriter writer,
        string entryPath,
        Stream source,
        CancellationToken cancellationToken = default
    ) => writer.WriteAsync(entryPath, source, null, cancellationToken);

    public static async Task WriteAsync(
        this IWriter writer,
        string entryPath,
        FileInfo source,
        CancellationToken cancellationToken = default
    )
    {
        if (!source.Exists)
        {
            throw new ArgumentException("Source does not exist: " + source.FullName);
        }
        using var stream = source.OpenRead();
        await writer
            .WriteAsync(entryPath, stream, source.LastWriteTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public static Task WriteAsync(
        this IWriter writer,
        string entryPath,
        string source,
        CancellationToken cancellationToken = default
    ) => writer.WriteAsync(entryPath, new FileInfo(source), cancellationToken);

    public static Task WriteAllAsync(
        this IWriter writer,
        string directory,
        string searchPattern = "*",
        SearchOption option = SearchOption.TopDirectoryOnly,
        CancellationToken cancellationToken = default
    ) => writer.WriteAllAsync(directory, searchPattern, null, option, cancellationToken);

    public static async Task WriteAllAsync(
        this IWriter writer,
        string directory,
        string searchPattern = "*",
        Func<string, bool>? fileSearchFunc = null,
        SearchOption option = SearchOption.TopDirectoryOnly,
        CancellationToken cancellationToken = default
    )
    {
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException("Directory does not exist: " + directory);
        }

        fileSearchFunc ??= n => true;
        foreach (
            var file in Directory
                .EnumerateFiles(directory, searchPattern, option)
                .Where(fileSearchFunc)
        )
        {
            await writer
                .WriteAsync(file.Substring(directory.Length), file, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public static Task WriteDirectoryAsync(
        this IWriter writer,
        string directoryName,
        CancellationToken cancellationToken = default
    ) => writer.WriteDirectoryAsync(directoryName, null, cancellationToken);
}
