using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Common.GZip;

internal sealed class GZipFilePart : FilePart
{
    private string? _name;
    private readonly Stream _stream;

    internal static GZipFilePart Create(Stream stream, IArchiveEncoding archiveEncoding)
    {
        var part = new GZipFilePart(stream, archiveEncoding);

        part.ReadAndValidateGzipHeader();
        if (stream.CanSeek)
        {
            var position = stream.Position;
            stream.Position = stream.Length - 8;
            part.ReadTrailer();
            stream.Position = position;
            part.EntryStartPosition = position;
        }
        else
        {
            // For non-seekable streams, we can't read the trailer or track position.
            // Set to 0 since the stream will be read sequentially from its current position.
            part.EntryStartPosition = 0;
        }
        return part;
    }

    internal static async ValueTask<GZipFilePart> CreateAsync(
        Stream stream,
        IArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken = default
    )
    {
        var part = new GZipFilePart(stream, archiveEncoding);

        await part.ReadAndValidateGzipHeaderAsync(cancellationToken);
        if (stream.CanSeek)
        {
            var position = stream.Position;
            stream.Position = stream.Length - 8;
            await part.ReadTrailerAsync(cancellationToken);
            stream.Position = position;
            part.EntryStartPosition = position;
        }
        else
        {
            // For non-seekable streams, we can't read the trailer or track position.
            // Set to 0 since the stream will be read sequentially from its current position.
            part.EntryStartPosition = 0;
        }
        return part;
    }

    private GZipFilePart(Stream stream, IArchiveEncoding archiveEncoding)
        : base(archiveEncoding) => _stream = stream;

    internal long EntryStartPosition { get; private set; }

    internal DateTime? DateModified { get; private set; }
    internal uint? Crc { get; private set; }
    internal uint? UncompressedSize { get; private set; }

    internal override string? FilePartName => _name;

    internal override Stream GetCompressedStream() =>
        new DeflateStream(
            _stream,
            CompressionMode.Decompress,
            CompressionLevel.Default,
            leaveOpen: true
        );

    internal override Stream GetRawStream() => _stream;

    private void ReadTrailer()
    {
        // Read and potentially verify the GZIP trailer: CRC32 and  size mod 2^32
        Span<byte> trailer = stackalloc byte[8];
        _stream.ReadFully(trailer);

        Crc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
        UncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer.Slice(4));
    }

    private async ValueTask ReadTrailerAsync(CancellationToken cancellationToken = default)
    {
        // Read and potentially verify the GZIP trailer: CRC32 and  size mod 2^32
        var trailer = new byte[8];
        _ = await _stream.ReadFullyAsync(trailer, 0, 8, cancellationToken);

        Crc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
        UncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer.AsSpan().Slice(4));
    }

    private void ReadAndValidateGzipHeader()
    {
        // read the header on the first read
        Span<byte> header = stackalloc byte[10];
        var n = _stream.Read(header);

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

        var timet = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4));
        DateModified = TarHeader.EPOCH.AddSeconds(timet);
        if ((header[3] & 0x04) == 0x04)
        {
            // read and discard extra field
            n = _stream.Read(header.Slice(0, 2)); // 2-byte length field

            var extraLength = (short)(header[0] + (header[1] * 256));
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
            _stream.ReadByte(); // CRC16, ignore
        }
    }

    private async ValueTask ReadAndValidateGzipHeaderAsync(
        CancellationToken cancellationToken = default
    )
    {
        // read the header on the first read
        var header = new byte[10];
        var n = await _stream.ReadAsync(header, 0, 10, cancellationToken);

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

        var timet = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan().Slice(4));
        DateModified = TarHeader.EPOCH.AddSeconds(timet);
        if ((header[3] & 0x04) == 0x04)
        {
            // read and discard extra field
            var lengthField = new byte[2];
            _ = await _stream.ReadAsync(lengthField, 0, 2, cancellationToken);

            var extraLength = (short)(lengthField[0] + (lengthField[1] * 256));
            var extra = new byte[extraLength];

            if (!await _stream.ReadFullyAsync(extra, cancellationToken))
            {
                throw new ZlibException("Unexpected end-of-file reading GZIP header.");
            }
        }
        if ((header[3] & 0x08) == 0x08)
        {
            _name = await ReadZeroTerminatedStringAsync(_stream, cancellationToken);
        }
        if ((header[3] & 0x10) == 0x010)
        {
            await ReadZeroTerminatedStringAsync(_stream, cancellationToken);
        }
        if ((header[3] & 0x02) == 0x02)
        {
            var buf = new byte[1];
            _ = await _stream.ReadAsync(buf, 0, 1, cancellationToken); // CRC16, ignore
        }
    }

    private string ReadZeroTerminatedString(Stream stream)
    {
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

    private async ValueTask<string> ReadZeroTerminatedStringAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var buf1 = new byte[1];
        var list = new List<byte>();
        var done = false;
        do
        {
            // workitem 7740
            var n = await stream.ReadAsync(buf1, 0, 1, cancellationToken);
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
