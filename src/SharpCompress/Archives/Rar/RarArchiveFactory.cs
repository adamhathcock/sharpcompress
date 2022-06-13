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
        public string Name => "Rar";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "rar";
            yield return "cbr";
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
