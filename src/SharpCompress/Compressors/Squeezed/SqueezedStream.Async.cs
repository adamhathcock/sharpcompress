using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.RLE90;

namespace SharpCompress.Compressors.Squeezed;

public partial class SqueezeStream
{
    public static async ValueTask<SqueezeStream> CreateAsync(
        Stream stream,
        int compressedSize,
        CancellationToken cancellationToken = default
    )
    {
        var squeezeStream = new SqueezeStream(stream, compressedSize);
        squeezeStream._decodedStream = await squeezeStream
            .BuildDecodedStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        return squeezeStream;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        return await _decodedStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        return await _decodedStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

    private async Task<Stream> BuildDecodedStreamAsync(CancellationToken cancellationToken)
    {
        byte[] numNodesBytes = new byte[2];
        int bytesRead = await _stream
            .ReadAsync(numNodesBytes, 0, 2, cancellationToken)
            .ConfigureAwait(false);

        if (bytesRead != 2)
        {
            return new MemoryStream(Array.Empty<byte>());
        }

        int numnodes = numNodesBytes[0] | (numNodesBytes[1] << 8);

        if (numnodes >= NUMVALS || numnodes == 0)
        {
            return new MemoryStream(Array.Empty<byte>());
        }

        var dnode = new int[numnodes, 2];
        for (int j = 0; j < numnodes; j++)
        {
            byte[] nodeBytes = new byte[4];
            bytesRead = await _stream
                .ReadAsync(nodeBytes, 0, 4, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead != 4)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }

            dnode[j, 0] = (short)(nodeBytes[0] | (nodeBytes[1] << 8));
            dnode[j, 1] = (short)(nodeBytes[2] | (nodeBytes[3] << 8));
        }

        var bitReader = new BitReader(_stream);
        var huffmanDecoded = new MemoryStream();
        int i = 0;

        while (true)
        {
            bool bit = await bitReader.ReadBitAsync(cancellationToken).ConfigureAwait(false);
            i = dnode[i, bit ? 1 : 0];
            if (i < 0)
            {
                i = -(i + 1);
                if (i == SPEOF)
                {
                    break;
                }
                huffmanDecoded.WriteByte((byte)i);
                i = 0;
            }
        }

        huffmanDecoded.Position = 0;
        return new RunLength90Stream(huffmanDecoded, (int)huffmanDecoded.Length);
    }
}
