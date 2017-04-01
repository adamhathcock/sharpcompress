using System;
using System.IO;
namespace SharpCompress.Writer
{
    public static class IWriterExtensions
    {
        public static void Write(this IWriter writer, string entryPath, Stream source)
        {
            writer.Write(entryPath, source, null);
        }

#if !PORTABLE
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

        public static void WriteAll(this IWriter writer, string directory, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly)
        {
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Directory does not exist: " + directory);
            }
#if THREEFIVE
            foreach (string file in Directory.GetFiles(directory, searchPattern, option))
#else
            foreach (string file in Directory.EnumerateFiles(directory, searchPattern, option))
#endif
            {
                writer.Write(file.Substring(directory.Length), file);
            }
        }
#endif
    }
}
