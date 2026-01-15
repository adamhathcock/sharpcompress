using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Ace.Headers;
using SharpCompress.Readers;
using SharpCompress.Readers.Ace;

namespace SharpCompress.Factories
{
    public class AceFactory : Factory, IReaderFactory
    {
        public override string Name => "Ace";

        public override ArchiveType? KnownArchiveType => ArchiveType.Ace;

        public override IEnumerable<string> GetSupportedExtensions()
        {
            yield return "ace";
        }

        public override bool IsArchive(
            Stream stream,
            string? password = null,
            int bufferSize = ReaderOptions.DefaultBufferSize
        ) => AceHeader.IsArchive(stream);

        public IReader OpenReader(Stream stream, ReaderOptions? options) =>
            AceReader.OpenReader(stream, options);

        public IAsyncReader OpenReaderAsync(
            Stream stream,
            ReaderOptions? options,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (IAsyncReader)AceReader.OpenReader(stream, options);
        }

        public override ValueTask<bool> IsArchiveAsync(
            Stream stream,
            string? password = null,
            int bufferSize = ReaderOptions.DefaultBufferSize
        ) => new(IsArchive(stream, password, bufferSize));
    }
}
