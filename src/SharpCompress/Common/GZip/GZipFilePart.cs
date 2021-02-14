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
            await ReadAndValidateGzipHeaderAsync(cancellationToken);
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

        internal override string FilePartName => _name!;

        internal override ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken)
        {
            return new(new DeflateStream(_stream, CompressionMode.Decompress, CompressionLevel.Default));
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

        private async ValueTask ReadAndValidateGzipHeaderAsync(CancellationToken cancellationToken)
        {
            // read the header on the first read
            using var header = MemoryPool<byte>.Shared.Rent(10);
            int n = await _stream.ReadAsync(header.Memory.Slice(0, 10), cancellationToken);

            // workitem 8501: handle edge case (decompress empty stream)
            if (n == 0)
            {
                return;
            }

            if (n != 10)
            {
                throw new ZlibException("Not a valid GZIP stream.");
            }

            if (header.Memory.Span[0] != 0x1F || header.Memory.Span[1] != 0x8B || header.Memory.Span[2] != 8)
            {
                throw new ZlibException("Bad GZIP header.");
            }

            int timet = BinaryPrimitives.ReadInt32LittleEndian(header.Memory.Span.Slice(4));
            DateModified = TarHeader.EPOCH.AddSeconds(timet);
            if ((header.Memory.Span[3] & 0x04) == 0x04)
            {
                // read and discard extra field
                n = await _stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken); // 2-byte length field

                short extraLength = (short)(header.Memory.Span[0] + header.Memory.Span[1] * 256);

                using var extra = MemoryPool<byte>.Shared.Rent(extraLength);
                if (await _stream.ReadAsync(extra.Memory.Slice(0, extraLength), cancellationToken) != extraLength)
                {
                    throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                }
                n = extraLength;
            }
            if ((header.Memory.Span[3] & 0x08) == 0x08)
            {
                _name = await ReadZeroTerminatedStringAsync(_stream, cancellationToken);
            }
            if ((header.Memory.Span[3] & 0x10) == 0x010)
            {
                await ReadZeroTerminatedStringAsync(_stream, cancellationToken);
            }
            if ((header.Memory.Span[3] & 0x02) == 0x02)
            {
                await _stream.ReadByteAsync(cancellationToken); // CRC16, ignore
            }
        }

        private async ValueTask<string> ReadZeroTerminatedStringAsync(Stream stream, CancellationToken cancellationToken)
        {
            var list = new List<byte>();
            bool done = false;
            do
            {
                // workitem 7740
                byte n = await stream.ReadByteAsync(cancellationToken);
                if (n != 1)
                {
                    throw new ZlibException("Unexpected EOF reading GZIP header.");
                }
                if (n == 0)
                {
                    done = true;
                }
                else
                {
                    list.Add(n);
                }
            }
            while (!done);
            byte[] buffer = list.ToArray();
            return ArchiveEncoding.Decode(buffer);
        }
    }
}
