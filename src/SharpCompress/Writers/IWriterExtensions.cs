using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Writers;

/// <summary>Convenience overloads and default filesystem writing workflows.</summary>
public static class IWriterExtensions
{
    extension(IWriter writer)
    {
        public void Write(string entryPath, Stream source) => writer.Write(entryPath, source, null);

        public void Write(string entryPath, FileInfo source) =>
            ClientDefaults.Client.FileWriter.WriteFile(writer, entryPath, source);

        public void Write(string entryPath, string source) =>
            writer.Write(entryPath, new FileInfo(source));

        public void WriteAll(
            string directory,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly
        ) =>
            ClientDefaults.Client.FileWriter.WriteDirectory(
                writer,
                directory,
                searchPattern,
                searchOption: option
            );

        public void WriteAll(
            string directory,
            string searchPattern = "*",
            Func<string, bool>? fileSearchFunc = null,
            SearchOption option = SearchOption.TopDirectoryOnly
        ) =>
            ClientDefaults.Client.FileWriter.WriteDirectory(
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

        public ValueTask WriteAsync(
            string entryPath,
            FileInfo source,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.FileWriter.WriteFileAsync(
                writer,
                entryPath,
                source,
                cancellationToken
            );

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
        ) =>
            ClientDefaults.Client.FileWriter.WriteDirectoryAsync(
                writer,
                directory,
                searchPattern,
                searchOption: option,
                cancellationToken: cancellationToken
            );

        public ValueTask WriteAllAsync(
            string directory,
            string searchPattern = "*",
            Func<string, bool>? fileSearchFunc = null,
            SearchOption option = SearchOption.TopDirectoryOnly,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.FileWriter.WriteDirectoryAsync(
                writer,
                directory,
                searchPattern,
                fileSearchFunc,
                option,
                cancellationToken
            );
    }
}
