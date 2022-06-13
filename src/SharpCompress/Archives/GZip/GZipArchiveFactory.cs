using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.GZip
{
    public class GZipArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> GetSupportedExtensions()
        {
            yield return new KeyValuePair<string, string>("GZip", "gz");
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream)
        {
            return GZipArchive.IsGZipFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return GZipArchive.Open(stream, readerOptions);
        }
    }
}
