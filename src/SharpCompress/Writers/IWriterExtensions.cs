#if !NO_FILE
using System;
#endif
using System.IO;

namespace SharpCompress.Writers
{
    public static class IWriterExtensions
    {

        public const string MatchAllPattern = "*";

        public static void Write(this IWriter writer, string entryPath, Stream source)
        {
            writer.Write(entryPath, source, null);
        }

#if !NO_FILE
        public static void Write(this IWriter writer, string entryPath, FileInfo source)
        {
            if (!source.Exists)
            {
                throw new ArgumentException("Source does not exist: " + source.FullName);
            }
            using (var stream = source.OpenRead())
            {
                writer.Write(entryPath, stream, source.LastWriteTime);
            }
        }

        public static void Write(this IWriter writer, string entryPath, string source)
        {
            writer.Write(entryPath, new FileInfo(source));
        }

        public static void WriteAll(this IWriter writer, string directory, string searchPattern = MatchAllPattern, SearchOption option = SearchOption.TopDirectoryOnly)
        {
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Directory does not exist: " + directory);
            }
#if NET35
            foreach (var file in Directory.GetDirectories(directory, searchPattern, option))
#else
            foreach (var file in Directory.EnumerateFiles(directory, searchPattern, option))
#endif
            {
                writer.Write(file.Substring(directory.Length), file);
            }
        }

#if !NET35
        /// <summary>
        /// Writes an entire directory (recursively) to a given archive (writer).
        /// </summary>
        /// <param name="writer">The writer writing to the archive.</param>
        /// <param name="directory">The directory to add to the archive.</param>
        /// <param name="searchPattern">The pattern used to select files/directories for archival.</param>
        /// <param name="option">The <see cref="SearchOption"/> used to select files/directories for archival.</param>
        /// <param name="prependDirectoryName">Sets whether the parent directory should be prepended to the path within the archive or not.</param>
        public static void WriteAll(this IWriter writer, DirectoryInfo directory, string searchPattern = MatchAllPattern, SearchOption option = SearchOption.TopDirectoryOnly, bool prependDirectoryName = false) {
            directory.Refresh();
            if (!directory.Exists) {
                throw new ArgumentException(string.Format("Directory does not exist: {0}", directory.FullName), nameof(directory));
            }

            var baseDir = directory.Name;
            var index = directory.FullName.IndexOf(baseDir);

            foreach (var file in directory.EnumerateFiles(searchPattern, option)) {
                writer.Write(prependDirectoryName ? file.FullName.Substring(index) : file.FullName.Substring(directory.FullName.Length), file);
            }

        }
#endif

#endif
    }
}