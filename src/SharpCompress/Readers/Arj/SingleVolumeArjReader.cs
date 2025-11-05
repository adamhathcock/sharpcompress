using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Arj;

namespace SharpCompress.Readers.Arj
{
    internal class SingleVolumeArjReader : ArjReader
    {
        private readonly Stream _stream;

        internal SingleVolumeArjReader(Stream stream, ReaderOptions options)
            : base(options)
        {
            stream.NotNull(nameof(stream));
            _stream = stream;
        }

        protected override Stream RequestInitialStream() => _stream;

        protected override void ValidateArchive(ArjVolume archive)
        {
            if (archive.IsMultiVolume)
            {
                throw new MultiVolumeExtractionException(
                    "Streamed archive is a Multi-volume archive. Use a different ArjReader method to extract."
                );
            }
        }
    }
}
