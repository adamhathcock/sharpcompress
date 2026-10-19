using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Arj.Headers;

public abstract partial class ArjHeader
{
    public abstract ValueTask<ArjHeader?> ReadAsync(
        Stream reader,
        CancellationToken cancellationToken = default
    );

    public async ValueTask<byte[]> ReadHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        // check for magic bytes
        var magic = new byte[2];
        if (!await stream.ReadFullyAsync(magic, cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<byte>();
        }

        if (!CheckMagicBytes(magic))
        {
            throw new InvalidFormatException("Not an ARJ file (wrong magic bytes)");
        }

        // read header_size
        byte[] headerBytes = new byte[2];
        await stream.ReadExactAsync(headerBytes, 0, 2, cancellationToken).ConfigureAwait(false);
        var headerSize = (ushort)(headerBytes[0] | headerBytes[1] << 8);
        if (headerSize < 1)
        {
            return Array.Empty<byte>();
        }

        var body = new byte[headerSize];
        if (!await stream.ReadFullyAsync(body, cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<byte>();
        }

        byte[] crc = new byte[4];
        await stream.ReadExactAsync(crc, 0, 4, cancellationToken).ConfigureAwait(false);
        var checksum = Crc32Stream.Compute(body);
        // Compute the hash value
        if (checksum != BitConverter.ToUInt32(crc, 0))
        {
            throw new InvalidFormatException("Header checksum is invalid");
        }
        return body;
    }

    protected async ValueTask<List<byte[]>> ReadExtendedHeadersAsync(
        Stream reader,
        CancellationToken cancellationToken = default
    )
    {
        List<byte[]> extendedHeader = new List<byte[]>();
        byte[] buffer = new byte[2];

        while (true)
        {
            if (!await reader.ReadFullyAsync(buffer, cancellationToken).ConfigureAwait(false))
            {
                throw new IncompleteArchiveException(
                    "Unexpected end of stream while reading extended header size."
                );
            }

            var extHeaderSize = (ushort)(buffer[0] | (buffer[1] << 8));
            if (extHeaderSize == 0)
            {
                return extendedHeader;
            }

            byte[] header = new byte[extHeaderSize];
            if (!await reader.ReadFullyAsync(header, cancellationToken).ConfigureAwait(false))
            {
                throw new IncompleteArchiveException(
                    "Unexpected end of stream while reading extended header data."
                );
            }

            byte[] crcextended = new byte[4];
            if (!await reader.ReadFullyAsync(crcextended, cancellationToken).ConfigureAwait(false))
            {
                throw new IncompleteArchiveException(
                    "Unexpected end of stream while reading extended header CRC."
                );
            }

            var checksum = Crc32Stream.Compute(header);
            if (checksum != BitConverter.ToUInt32(crcextended, 0))
            {
                throw new InvalidFormatException("Extended header checksum is invalid");
            }

            extendedHeader.Add(header);
        }
    }

    /// <summary>
    /// Asynchronously checks if the stream is an ARJ archive
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the stream is an ARJ archive, false otherwise</returns>
    public static async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = new byte[2];
        if (!await stream.ReadFullyAsync(bytes, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return CheckMagicBytes(bytes);
    }
}
