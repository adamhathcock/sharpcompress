using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Writers
{
    public static class IWriterExtensions
    {
        public static Task WriteAsync(this IWriter writer, string entryPath, Stream source, CancellationToken cancellationToken = default)
        {
            return writer.WriteAsync(entryPath, source, null, cancellationToken);
        }

        public static async Task WriteAsync(this IWriter writer, string entryPath, FileInfo source, CancellationToken cancellationToken = default)
        {
            if (!source.Exists)
            {
                throw new ArgumentException("Source does not exist: " + source.FullName);
            }
            using (var stream = source.OpenRead())
            {
                await writer.WriteAsync(entryPath, stream, source.LastWriteTime, cancellationToken);
            }
        }

        public static Task WriteAsync(this IWriter writer, string entryPath, string source, CancellationToken cancellationToken = default)
        {
            return writer.WriteAsync(entryPath, new FileInfo(source), cancellationToken);
        }

        public static Task WriteAllAsync(this IWriter writer, string directory, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
        {
            return writer.WriteAllAsync(directory, searchPattern, null, option, cancellationToken);
        }

        public static async Task WriteAllAsync(this IWriter writer,
                                         string directory,
                                         string searchPattern = "*",
                                         Expression<Func<string, bool>>? fileSearchFunc = null,
                                         SearchOption option = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Directory does not exist: " + directory);
            }

            if (fileSearchFunc is null)
            {
                fileSearchFunc = n => true;
            }
            foreach (var file in Directory.EnumerateFiles(directory, searchPattern, option).Where(fileSearchFunc.Compile()))
            {
                await writer.WriteAsync(file.Substring(directory.Length), file, cancellationToken);
            }
        }
    }
}