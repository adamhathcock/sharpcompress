﻿#if !NO_FILE
using System;
#endif
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace SharpCompress.Writers
{
    public static class IWriterExtensions
    {
        public static void Write(this IWriter writer, string entryPath, Stream source)
        {
            writer.Write(entryPath, source, null);
        }

#if !NO_FILE
        public static void Write(this IWriter writer, string entryPath, FileInfo source, bool contentOnly = false)
        {
            if (!source.Exists)
            {
                throw new ArgumentException("Source does not exist: " + source.FullName);
            }
            using (var stream = source.OpenRead())
            {
                var modified = contentOnly ? (DateTime?)null : source.LastWriteTime;
                writer.Write(entryPath, stream, modified);
            }
        }

        public static void Write(this IWriter writer, string entryPath, string source, bool contentOnly = false)
        {
            writer.Write(entryPath, new FileInfo(source), contentOnly);
        }

        public static void WriteAll(
            this IWriter writer,
            string directory,
            string searchPattern = "*",
            SearchOption option = SearchOption.TopDirectoryOnly,
            bool contentOnly = false)
        {
            writer.WriteAll(directory, searchPattern, null, option, contentOnly);
        }

        public static void WriteAll(
            this IWriter writer,
            string directory,
            string searchPattern = "*",
            Expression<Func<string, bool>> fileSearchFunc = null,
            SearchOption option = SearchOption.TopDirectoryOnly,
            bool contentOnly = false)
        {
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Directory does not exist: " + directory);
            }

            if (fileSearchFunc == null)
            {
                fileSearchFunc = n => true;
            }
#if NET35
            IEnumerable<string> paths = Directory.GetFiles(directory, searchPattern, option);
#else
            IEnumerable<string> paths = Directory.EnumerateFiles(directory, searchPattern, option);
#endif
            paths = paths.Where(fileSearchFunc.Compile());

            if (contentOnly)
            {
                var pathList = paths.ToList();
                pathList.Sort();
                paths = pathList;
            }

            foreach (var path in paths)
            {
                writer.Write(path.Substring(directory.Length), path, contentOnly);
            }
        }

#endif
    }
}