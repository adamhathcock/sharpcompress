using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip
{
    public class SevenZipArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> GetSupportedExtensions()
        {
            yield return new KeyValuePair<string, string>("7Zip", "7z");
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream)
        {
            return SevenZipArchive.IsSevenZipFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return SevenZipArchive.Open(stream, readerOptions);
        }
    }
}
