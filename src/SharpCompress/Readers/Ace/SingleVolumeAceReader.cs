using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Ace;

namespace SharpCompress.Readers.Ace
{
    internal class SingleVolumeAceReader : AceReader
    {
        private readonly Stream _stream;

        internal SingleVolumeAceReader(Stream stream, ReaderOptions options)
            : base(options)
        {
            stream.NotNull(nameof(stream));
            _stream = stream;
        }

        protected override Stream RequestInitialStream() => _stream;

        protected override void ValidateArchive(AceVolume archive)
        {
            if (archive.IsMultiVolume)
            {
                throw new MultiVolumeExtractionException(
                    "Streamed archive is a Multi-volume archive. Use a different AceReader method to extract."
                );
            }
        }
    }
}
