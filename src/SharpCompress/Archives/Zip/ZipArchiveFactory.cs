using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.Zip
{
    public class ZipArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public string Name => "Zip";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "zip";
            yield return "zipx";
            yield return "cbz";
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream)
        {
            return ZipArchive.IsZipFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return ZipArchive.Open(stream, readerOptions);
        }
    }
}
