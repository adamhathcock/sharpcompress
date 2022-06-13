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
        public string Name => "7Zip";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "7z";
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
