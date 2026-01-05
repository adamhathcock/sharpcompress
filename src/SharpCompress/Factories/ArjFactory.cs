using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Readers;
using SharpCompress.Readers.Arj;

namespace SharpCompress.Factories
{
    public class ArjFactory : Factory, IReaderFactory
    {
        public override string Name => "Arj";

        public override ArchiveType? KnownArchiveType => ArchiveType.Arj;

        public override IEnumerable<string> GetSupportedExtensions()
        {
            yield return "arj";
        }

        public override bool IsArchive(
            Stream stream,
            string? password = null,
            int bufferSize = ReaderOptions.DefaultBufferSize
        )
        {
            return ArjHeader.IsArchive(stream);
        }

        public IReader OpenReader(Stream stream, ReaderOptions? options) =>
            ArjReader.Open(stream, options);
    }
}
