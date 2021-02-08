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
                ReadTrailer();
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

        private void ReadTrailer()
        {
            // Read and potentially verify the GZIP trailer: CRC32 and  size mod 2^32
            Span<byte> trailer = stackalloc byte[8];
            int n = _stream.Read(trailer);

            Crc = BinaryPrimitives.ReadInt32LittleEndian(trailer);
            UncompressedSize = BinaryPrimitives.ReadInt32LittleEndian(trailer.Slice(4));
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
                if (!await _stream.ReadFullyAsync(extra.Memory.Slice(0, extraLength), cancellationToken))
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
                using var one = MemoryPool<byte>.Shared.Rent(1);
                await _stream.ReadAsync(one.Memory.Slice(0,1), cancellationToken); // CRC16, ignore
            }
        }

        private async ValueTask<string> ReadZeroTerminatedStringAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var buf1 = MemoryPool<byte>.Shared.Rent(1);
            var list = new List<byte>();
            bool done = false;
            do
            {
                // workitem 7740
                int n = await stream.ReadAsync(buf1.Memory.Slice(0, 1), cancellationToken);
                if (n != 1)
                {
                    throw new ZlibException("Unexpected EOF reading GZIP header.");
                }
                if (buf1.Memory.Span[0] == 0)
                {
                    done = true;
                }
                else
                {
                    list.Add(buf1.Memory.Span[0]);
                }
            }
            while (!done);
            byte[] buffer = list.ToArray();
            return ArchiveEncoding.Decode(buffer);
        }
    }
}
