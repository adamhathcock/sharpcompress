using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar
{
    public class RarArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> GetSupportedExtensions()
        {
            yield return new KeyValuePair<string, string>("Rar", "rar");
            yield return new KeyValuePair<string, string>("Rar", "cbr");
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream)
        {
            return RarArchive.IsRarFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return RarArchive.Open(stream, readerOptions);
        }
    }
}
