using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.Tar
{
    public class TarArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> GetSupportedExtensions()
        {
            yield return new KeyValuePair<string, string>("Tar", "tar");
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream)
        {
            return TarArchive.IsTarFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return TarArchive.Open(stream, readerOptions);
        }
    }
}
