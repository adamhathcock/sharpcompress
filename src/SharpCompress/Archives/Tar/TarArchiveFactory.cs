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
        public string Name => "Tar";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "tar";
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
