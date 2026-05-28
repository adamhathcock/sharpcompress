using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Ace.Headers;

public abstract partial class AceHeader
{
    public abstract ValueTask<AceHeader?> ReadAsync(
        Stream reader,
        CancellationToken cancellationToken = default
    );

    public async ValueTask<byte[]> ReadHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        // Read header CRC (2 bytes) and header size (2 bytes)
        var headerBytes = new byte[4];
        if (
            !await stream.ReadFullyAsync(headerBytes, 0, 4, cancellationToken).ConfigureAwait(false)
        )
        {
            return Array.Empty<byte>();
        }

        HeaderCrc = BitConverter.ToUInt16(headerBytes, 0); // CRC for validation
        HeaderSize = BitConverter.ToUInt16(headerBytes, 2);
        if (HeaderSize == 0)
        {
            return Array.Empty<byte>();
        }

        // Read the header data
        var body = new byte[HeaderSize];
        if (
            !await stream
                .ReadFullyAsync(body, 0, HeaderSize, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return Array.Empty<byte>();
        }

        // Verify crc
        var checksum = AceCrc.AceCrc16(body);
        if (checksum != HeaderCrc)
        {
            throw new InvalidFormatException("Header checksum is invalid");
        }
        return body;
    }

    /// <summary>
    /// Asynchronously checks if the stream is an ACE archive
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the stream is an ACE archive, false otherwise</returns>
    public static async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = new byte[14];
        if (!await stream.ReadFullyAsync(bytes, 0, 14, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return CheckMagicBytes(bytes, 7);
    }
}
