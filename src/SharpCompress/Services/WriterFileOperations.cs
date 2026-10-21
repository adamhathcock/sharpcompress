using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pure.DI;
using SharpCompress.Writers;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed class WriterFileOperations : IWriterFileOperations
{
    public void Write(IWriter writer, string entryPath, FileInfo source)
    {
        if (!source.Exists)
        {
            throw new ArgumentException("Source does not exist: " + source.FullName);
        }
        using var stream = source.OpenRead();
        writer.Write(entryPath, stream, source.LastWriteTime);
    }

    public async ValueTask WriteAsync(
        IAsyncWriter writer,
        string entryPath,
        FileInfo source,
        CancellationToken cancellationToken = default
    )
    {
        if (!source.Exists)
        {
            throw new ArgumentException("Source does not exist: " + source.FullName);
        }
        var stream = source.OpenRead();
        await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
        await writer
            .WriteAsync(entryPath, stream, source.LastWriteTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public void WriteAll(
        IWriter writer,
        string directory,
        string searchPattern,
        Func<string, bool>? fileSearchFunc,
        SearchOption option
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
            Write(writer, file.Substring(directory.Length), new FileInfo(file));
        }
    }

    public async ValueTask WriteAllAsync(
        IAsyncWriter writer,
        string directory,
        string searchPattern,
        Func<string, bool>? fileSearchFunc,
        SearchOption option,
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
            await WriteAsync(
                    writer,
                    file.Substring(directory.Length),
                    new FileInfo(file),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }
}
