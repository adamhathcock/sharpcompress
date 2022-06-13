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
        public IEnumerable<KeyValuePair<string, string>> GetSupportedExtensions()
        {
            yield return new KeyValuePair<string, string>("Zip", "zip");
            yield return new KeyValuePair<string, string>("Zip", "zipx");
            yield return new KeyValuePair<string, string>("Zip", "cbz");
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
