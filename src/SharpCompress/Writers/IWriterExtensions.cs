using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Writers;

public static class IWriterExtensions
{
    extension(IWriter writer)
    {
        public void Write(string entryPath, Stream source) =>
            writer.Write(entryPath, source, null);

        public void Write(string entryPath, FileInfo source)
        {
            if (!source.Exists)
            {
                throw new ArgumentException("Source does not exist: " + source.FullName);
            }

            using var stream = source.OpenRead();
            writer.Write(entryPath, stream, source.LastWriteTime);
        }

        public void Write(string entryPath, string source) =>
            writer.Write(entryPath, new FileInfo(source));

        public void WriteAll(string directory,
                             string searchPattern = "*",
                             SearchOption option = SearchOption.TopDirectoryOnly
        ) => writer.WriteAll(directory, searchPattern, null, option);

        public void WriteAll(string directory,
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

        public void WriteDirectory(string directoryName) =>
            writer.WriteDirectory(directoryName, null);
    }

    extension(IAsyncWriter writer)
    {
        public ValueTask WriteAsync(string entryPath,
                                Stream source,
                                CancellationToken cancellationToken = default
        ) => writer.WriteAsync(entryPath, source, null, cancellationToken);

        public async ValueTask WriteAsync(string entryPath,
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

        public ValueTask WriteAsync(string entryPath,
                                    string source,
                                    CancellationToken cancellationToken = default
        ) => writer.WriteAsync(entryPath, new FileInfo(source), cancellationToken);

        public ValueTask WriteAllAsync(string directory,
                                       string searchPattern = "*",
                                       SearchOption option = SearchOption.TopDirectoryOnly,
                                       CancellationToken cancellationToken = default
        ) => writer.WriteAllAsync(directory, searchPattern, null, option, cancellationToken);

        public async ValueTask WriteAllAsync(string directory,
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

        public ValueTask WriteDirectoryAsync(string directoryName,
                                             CancellationToken cancellationToken = default
        ) => writer.WriteDirectoryAsync(directoryName, null, cancellationToken);
    }

    // Async extensions
}
