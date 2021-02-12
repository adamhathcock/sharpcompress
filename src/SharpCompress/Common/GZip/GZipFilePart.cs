using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Common.GZip
{
    internal sealed class GZipFilePart : FilePart
    {
        private string? _name;
        private readonly Stream _stream;

        internal GZipFilePart(Stream stream, ArchiveEncoding archiveEncoding)
            : base(archiveEncoding)
        {
            _stream = stream;
            ReadAndValidateGzipHeader();
            if (stream.CanSeek)
            {
                long position = stream.Position;
                stream.Position = stream.Length - 8;
                ReadTrailer();
                stream.Position = position;
            }
            EntryStartPosition = stream.Position;
        }

        internal long EntryStartPosition { get; }

        internal DateTime? DateModified { get; private set; }
        internal int? Crc { get; private set; }
        internal int? UncompressedSize { get; private set; }

        internal override string FilePartName => _name!;

        internal override Stream GetCompressedStream()
        {
            return new DeflateStream(_stream, CompressionMode.Decompress, CompressionLevel.Default);
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

        private void ReadAndValidateGzipHeader()
        {
            // read the header on the first read
            Span<byte> header = stackalloc byte[10];
            int n = _stream.Read(header);

            // workitem 8501: handle edge case (decompress empty stream)
            if (n == 0)
            {
                return;
            }

            if (n != 10)
            {
                throw new ZlibException("Not a valid GZIP stream.");
            }

            if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
            {
                throw new ZlibException("Bad GZIP header.");
            }

            int timet = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4));
            DateModified = TarHeader.EPOCH.AddSeconds(timet);
            if ((header[3] & 0x04) == 0x04)
            {
                // read and discard extra field
                n = _stream.Read(header.Slice(0, 2)); // 2-byte length field

                short extraLength = (short)(header[0] + header[1] * 256);
                byte[] extra = new byte[extraLength];

                if (!_stream.ReadFully(extra))
                {
                    throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                }
                n = extraLength;
            }
            if ((header[3] & 0x08) == 0x08)
            {
                _name = ReadZeroTerminatedString(_stream);
            }
            if ((header[3] & 0x10) == 0x010)
            {
                ReadZeroTerminatedString(_stream);
            }
            if ((header[3] & 0x02) == 0x02)
            {
                _stream.ReadByte(); // CRC16, ignore
            }
        }

        private string ReadZeroTerminatedString(Stream stream)
        {
            Span<byte> buf1 = stackalloc byte[1];
            var list = new List<byte>();
            bool done = false;
            do
            {
                // workitem 7740
                int n = stream.Read(buf1);
                if (n != 1)
                {
                    throw new ZlibException("Unexpected EOF reading GZIP header.");
                }
                if (buf1[0] == 0)
                {
                    done = true;
                }
                else
                {
                    list.Add(buf1[0]);
                }
            }
            while (!done);
            byte[] buffer = list.ToArray();
            return ArchiveEncoding.Decode(buffer);
        }
    }
}
