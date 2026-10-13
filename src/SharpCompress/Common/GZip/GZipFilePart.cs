using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Common.GZip;

internal sealed partial class GZipFilePart : FilePart
{
    private string? _name;
    private readonly Stream _stream;
    private readonly CompressionProviderRegistry _compressionProviders;
    private Stream? _decompressor;

    private GZipFilePart(
        Stream stream,
        IArchiveEncoding archiveEncoding,
        CompressionProviderRegistry compressionProviders
    )
        : base(archiveEncoding)
    {
        _stream = SharpCompressStream.Create(stream);
        _compressionProviders = compressionProviders;
    }

    internal long EntryStartPosition { get; private set; }

    internal DateTime? DateModified { get; private set; }
    internal uint? Crc { get; private set; }
    internal uint? UncompressedSize { get; private set; }

    internal override string? FilePartName => _name;

    internal override Stream GetCompressedStream()
    {
        //GZip uses Deflate compression, at this point we need a deflate stream
        _decompressor = _compressionProviders.CreateDecompressStream(
            CompressionType.Deflate,
            _stream
        );
        return _decompressor;
    }

    internal Stream WrapWithChecksumValidation(Stream source, string? entryName) =>
        new GZipChecksumValidationStream(
            source,
            _stream,
            entryName,
            Crc,
            UncompressedSize,
            () => (_decompressor as DeflateStream)?.InputBuffer
        );

    internal override Stream GetRawStream() => _stream;

    private void ReadTrailer()
    {
        // Keep this sync implementation for its stack allocation on the parser hot path.
        // Read and potentially verify the GZIP trailer: CRC32 and  size mod 2^32
        Span<byte> trailer = stackalloc byte[8];
        if (!_stream.ReadFully(trailer))
        {
            throw new ZlibException("Unexpected end-of-file reading GZIP trailer.");
        }

        Crc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
        UncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer.Slice(4));
    }

    private void ReadAndValidateGzipHeader()
    {
        // Keep this sync implementation for stackalloc and ReadByte-based header parsing.
        // read the header on the first read
        Span<byte> header = stackalloc byte[10];
        var n = _stream.Read(header);

        // workitem 8501: handle edge case (decompress empty stream)
        if (n == 0)
        {
            return;
        }

        if (!_stream.ReadFully(header.Slice(n)))
        {
            throw new ZlibException("Not a valid GZIP stream.");
        }

        if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
        {
            throw new ZlibException("Bad GZIP header.");
        }

        var timet = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4));
        DateModified = TarHeader.EPOCH.AddSeconds(timet);
        if ((header[3] & 0x04) == 0x04)
        {
            // read and discard extra field
            if (!_stream.ReadFully(header.Slice(0, 2)))
            {
                throw new ZlibException("Unexpected end-of-file reading GZIP header.");
            }

            var extraLength = header[0] + (header[1] * 256);
            var extra = new byte[extraLength];

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
            Span<byte> headerCrc = stackalloc byte[2];
            if (!_stream.ReadFully(headerCrc))
            {
                throw new ZlibException("Unexpected end-of-file reading GZIP header CRC.");
            }
        }
    }

    private string ReadZeroTerminatedString(Stream stream)
    {
        // Keep this sync implementation for its one-byte stack allocation.
        Span<byte> buf1 = stackalloc byte[1];
        var list = new List<byte>();
        var done = false;
        do
        {
            // workitem 7740
            var n = stream.Read(buf1);
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
        } while (!done);
        var buffer = list.ToArray();
        return ArchiveEncoding.Decode(buffer);
    }
}
