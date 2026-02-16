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
        if (await stream.ReadAsync(magic, 0, 2, cancellationToken).ConfigureAwait(false) != 2)
        {
            return Array.Empty<byte>();
        }

        if (!CheckMagicBytes(magic))
        {
            throw new InvalidFormatException("Not an ARJ file (wrong magic bytes)");
        }

        // read header_size
        byte[] headerBytes = new byte[2];
        await stream.ReadAsync(headerBytes, 0, 2, cancellationToken).ConfigureAwait(false);
        var headerSize = (ushort)(headerBytes[0] | headerBytes[1] << 8);
        if (headerSize < 1)
        {
            return Array.Empty<byte>();
        }

        var body = new byte[headerSize];
        var read = await stream
            .ReadAsync(body, 0, headerSize, cancellationToken)
            .ConfigureAwait(false);
        if (read < headerSize)
        {
            return Array.Empty<byte>();
        }

        byte[] crc = new byte[4];
        await stream.ReadFullyAsync(crc, 0, 4, cancellationToken).ConfigureAwait(false);
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
            int bytesRead = await reader
                .ReadAsync(buffer, 0, 2, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead < 2)
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
            bytesRead = await reader
                .ReadAsync(header, 0, extHeaderSize, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead < extHeaderSize)
            {
                throw new IncompleteArchiveException(
                    "Unexpected end of stream while reading extended header data."
                );
            }

            byte[] crcextended = new byte[4];
            bytesRead = await reader
                .ReadAsync(crcextended, 0, 4, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead < 4)
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
        if (await stream.ReadAsync(bytes, 0, 2, cancellationToken).ConfigureAwait(false) != 2)
        {
            return false;
        }

        return CheckMagicBytes(bytes);
    }
}
