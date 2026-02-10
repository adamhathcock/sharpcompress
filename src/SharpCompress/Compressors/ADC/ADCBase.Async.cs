using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.ADC;

public static partial class ADCBase
{
    /// <summary>
    /// Decompresses a byte buffer asynchronously that's compressed with ADC
    /// </summary>
    /// <param name="input">Compressed buffer</param>
    /// <param name="bufferSize">Max size for decompressed data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing bytes read and decompressed data</returns>
    public static async ValueTask<AdcDecompressResult> DecompressAsync(
        byte[] input,
        int bufferSize = 262144,
        CancellationToken cancellationToken = default
    ) =>
        await DecompressAsync(new MemoryStream(input), bufferSize, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Decompresses a stream asynchronously that's compressed with ADC
    /// </summary>
    /// <param name="input">Stream containing compressed data</param>
    /// <param name="bufferSize">Max size for decompressed data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing bytes read and decompressed data</returns>
    public static async ValueTask<AdcDecompressResult> DecompressAsync(
        Stream input,
        int bufferSize = 262144,
        CancellationToken cancellationToken = default
    )
    {
        var result = new AdcDecompressResult();

        if (input is null || input.Length == 0)
        {
            result.BytesRead = 0;
            result.Output = null;
            return result;
        }

        var start = (int)input.Position;
        var position = (int)input.Position;
        int chunkSize;
        int offset;
        int chunkType;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        var outPosition = 0;
        var full = false;
        byte[] temp = ArrayPool<byte>.Shared.Rent(3);

        try
        {
            while (position < input.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readByte = input.ReadByte();
                if (readByte == -1)
                {
                    break;
                }

                chunkType = GetChunkType((byte)readByte);

                switch (chunkType)
                {
                    case PLAIN:
                        chunkSize = GetChunkSize((byte)readByte);
                        if (outPosition + chunkSize > bufferSize)
                        {
                            full = true;
                            break;
                        }

                        var readCount = await input
                            .ReadAsync(buffer, outPosition, chunkSize, cancellationToken)
                            .ConfigureAwait(false);
                        outPosition += readCount;
                        position += readCount + 1;
                        break;
                    case TWO_BYTE:
                        chunkSize = GetChunkSize((byte)readByte);
                        temp[0] = (byte)readByte;
                        temp[1] = (byte)input.ReadByte();
                        offset = GetOffset(temp.AsSpan(0, 2));
                        if (outPosition + chunkSize > bufferSize)
                        {
                            full = true;
                            break;
                        }

                        if (offset == 0)
                        {
                            var lastByte = buffer[outPosition - 1];
                            for (var i = 0; i < chunkSize; i++)
                            {
                                buffer[outPosition] = lastByte;
                                outPosition++;
                            }

                            position += 2;
                        }
                        else
                        {
                            for (var i = 0; i < chunkSize; i++)
                            {
                                buffer[outPosition] = buffer[outPosition - offset - 1];
                                outPosition++;
                            }

                            position += 2;
                        }

                        break;
                    case THREE_BYTE:
                        chunkSize = GetChunkSize((byte)readByte);
                        temp[0] = (byte)readByte;
                        temp[1] = (byte)input.ReadByte();
                        temp[2] = (byte)input.ReadByte();
                        offset = GetOffset(temp.AsSpan(0, 3));
                        if (outPosition + chunkSize > bufferSize)
                        {
                            full = true;
                            break;
                        }

                        if (offset == 0)
                        {
                            var lastByte = buffer[outPosition - 1];
                            for (var i = 0; i < chunkSize; i++)
                            {
                                buffer[outPosition] = lastByte;
                                outPosition++;
                            }

                            position += 3;
                        }
                        else
                        {
                            for (var i = 0; i < chunkSize; i++)
                            {
                                buffer[outPosition] = buffer[outPosition - offset - 1];
                                outPosition++;
                            }

                            position += 3;
                        }

                        break;
                }

                if (full)
                {
                    break;
                }
            }

            var output = new byte[outPosition];
            Array.Copy(buffer, output, outPosition);
            result.BytesRead = position - start;
            result.Output = output;
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<byte>.Shared.Return(temp);
        }
    }
}
