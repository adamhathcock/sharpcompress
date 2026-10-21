using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Services;

namespace SharpCompress.Writers;

public static class IWriterExtensions
{
    extension(IWriter writer)
    {
        public void Write(string entryPath, Stream source) => writer.Write(entryPath, source, null);

        public void Write(string entryPath, FileInfo source) =>
            LibraryDefaults.Composition.WriterFiles.Write(writer, entryPath, source);

        public void Write(string entryPath, string source) =>
            writer.Write(entryPath, new FileInfo(source));

        public void WriteAll(
            string directory,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly
        ) => writer.WriteAll(directory, searchPattern, null, option);

        public void WriteAll(
            string directory,
            string searchPattern = "*",
            Func<string, bool>? fileSearchFunc = null,
            SearchOption option = SearchOption.TopDirectoryOnly
        ) =>
            LibraryDefaults.Composition.WriterFiles.WriteAll(
                writer,
                directory,
                searchPattern,
                fileSearchFunc,
                option
            );

        public void WriteDirectory(string directoryName) =>
            writer.WriteDirectory(directoryName, null);
    }

    extension(IAsyncWriter writer)
    {
        public ValueTask WriteAsync(
            string entryPath,
            Stream source,
            CancellationToken cancellationToken = default
        ) => writer.WriteAsync(entryPath, source, null, cancellationToken);

        public async ValueTask WriteAsync(
            string entryPath,
            FileInfo source,
            CancellationToken cancellationToken = default
        ) =>
            await LibraryDefaults
                .Composition.WriterFiles.WriteAsync(writer, entryPath, source, cancellationToken)
                .ConfigureAwait(false);

        public ValueTask WriteAsync(
            string entryPath,
            string source,
            CancellationToken cancellationToken = default
        ) => writer.WriteAsync(entryPath, new FileInfo(source), cancellationToken);

        public ValueTask WriteAllAsync(
            string directory,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly,
            CancellationToken cancellationToken = default
        ) => writer.WriteAllAsync(directory, searchPattern, null, option, cancellationToken);

        public async ValueTask WriteAllAsync(
            string directory,
            string searchPattern = "*",
            Func<string, bool>? fileSearchFunc = null,
            SearchOption option = SearchOption.TopDirectoryOnly,
            CancellationToken cancellationToken = default
        ) =>
            await LibraryDefaults
                .Composition.WriterFiles.WriteAllAsync(
                    writer,
                    directory,
                    searchPattern,
                    fileSearchFunc,
                    option,
                    cancellationToken
                )
                .ConfigureAwait(false);

        public ValueTask WriteDirectoryAsync(
            string directoryName,
            CancellationToken cancellationToken = default
        ) => writer.WriteDirectoryAsync(directoryName, null, cancellationToken);
    }
}
