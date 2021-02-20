using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Common.GZip
{
    internal sealed class GZipFilePart : FilePart
    {
        private string? _name;
        //init only
#nullable disable
        private Stream _stream;
#nullable enable

        internal GZipFilePart(ArchiveEncoding archiveEncoding)
            : base(archiveEncoding)
        {
        }

        internal async ValueTask Initialize(Stream stream, CancellationToken cancellationToken)
        {
            _stream = stream;
            if (stream.CanSeek)
            {
                long position = stream.Position;
                stream.Position = stream.Length - 8;
                await ReadTrailerAsync(cancellationToken);
                stream.Position = position;
            }
            EntryStartPosition = stream.Position;
        }

        internal long EntryStartPosition { get; private set; }

        internal DateTime? DateModified { get; private set; }
        internal int? Crc { get; private set; }
        internal int? UncompressedSize { get; private set; }

        internal override string? FilePartName => _name;

        internal override async ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken)
        {
            var stream = new GZipStream(_stream, CompressionMode.Decompress, CompressionLevel.Default);
            await stream.ReadAsync(Array.Empty<byte>(), 0, 0, cancellationToken);
            _name = stream.FileName;
            DateModified = stream.LastModified;
            return stream;
        }

        internal override Stream GetRawStream()
        {
            return _stream;
        }

        private async ValueTask ReadTrailerAsync(CancellationToken cancellationToken)
        {
            // Read and potentially verify the GZIP trailer: CRC32 and  size mod 2^32

            Crc = await _stream.ReadInt32(cancellationToken);
            UncompressedSize = await _stream.ReadInt32(cancellationToken);
        }
    }
}
