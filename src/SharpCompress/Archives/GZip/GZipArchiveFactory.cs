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
        public string Name => "GZip";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "gz";
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
