using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Crypto;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Result of compressing a stream - contains folder metadata, compressed sizes, and CRCs.
/// </summary>
internal sealed class PackedStream
{
    public CFolder Folder { get; init; } = new();
    public ulong[] Sizes { get; init; } = [];
    public uint?[] CRCs { get; init; } = [];
}

/// <summary>
/// Compresses a single input stream using LZMA or LZMA2, writing compressed output
/// to the archive stream. Builds the CFolder metadata describing the compression.
/// Uses SharpCompress's existing LzmaStream encoder.
/// </summary>
internal sealed class SevenZipStreamsCompressor(Stream outputStream)
{
    /// <summary>
    /// Compresses the input stream to the output stream using the specified method.
    /// Returns a PackedStream containing folder metadata, compressed size, and CRCs.
    /// </summary>
    /// <param name="inputStream">Uncompressed data to compress.</param>
    /// <param name="compressionType">Compression method (LZMA or LZMA2).</param>
    /// <param name="encoderProperties">LZMA encoder properties (null for defaults).</param>
    public PackedStream Compress(
        Stream inputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null
    )
    {
        var isLzma2 = compressionType == CompressionType.LZMA2;
        encoderProperties ??= new LzmaEncoderProperties(eos: !isLzma2);

        var outStartOffset = outputStream.Position;

        // Wrap the output stream in CRC calculator
        using var outCrcStream = new Crc32Stream(outputStream);

        byte[] properties;

        if (isLzma2)
        {
            // LZMA2: use Lzma2EncoderStream for chunk-based framing
            using var lzma2Stream = new Lzma2EncoderStream(
                outCrcStream,
                encoderProperties.DictionarySize,
                encoderProperties.NumFastBytes
            );

            CopyWithCrc(inputStream, lzma2Stream, out var inputCrc2, out var inputSize2);
            lzma2Stream.Dispose();

            properties = lzma2Stream.Properties;

            return BuildPackedStream(
                isLzma2: true,
                properties,
                (ulong)(outputStream.Position - outStartOffset),
                (ulong)inputSize2,
                inputCrc2,
                outCrcStream.Crc
            );
        }

        // LZMA
        using var lzmaStream = LzmaStream.Create(encoderProperties, false, outCrcStream);
        properties = lzmaStream.Properties;

        CopyWithCrc(inputStream, lzmaStream, out var inputCrc, out var inputSize);
        lzmaStream.Dispose();

        return BuildPackedStream(
            isLzma2: false,
            properties,
            (ulong)(outputStream.Position - outStartOffset),
            (ulong)inputSize,
            inputCrc,
            outCrcStream.Crc
        );
    }

    /// <summary>
    /// Asynchronously compresses the input stream to the output stream using the specified method.
    /// Returns a PackedStream containing folder metadata, compressed size, and CRCs.
    /// </summary>
    /// <param name="inputStream">Uncompressed data to compress.</param>
    /// <param name="compressionType">Compression method (LZMA or LZMA2).</param>
    /// <param name="encoderProperties">LZMA encoder properties (null for defaults).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<PackedStream> CompressAsync(
        Stream inputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isLzma2 = compressionType == CompressionType.LZMA2;
        encoderProperties ??= new LzmaEncoderProperties(eos: !isLzma2);

        var outStartOffset = outputStream.Position;

        // Wrap the output stream in CRC calculator
        using var outCrcStream = new Crc32Stream(outputStream);

        byte[] properties;

        if (isLzma2)
        {
            // LZMA2: use Lzma2EncoderStream for chunk-based framing
            uint inputCrc2;
            long inputSize2;
            {
                await using var lzma2Stream = new Lzma2EncoderStream(
                    outCrcStream,
                    encoderProperties.DictionarySize,
                    encoderProperties.NumFastBytes
                );

                (inputCrc2, inputSize2) = await CopyWithCrcAsync(
                        inputStream,
                        lzma2Stream,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                properties = lzma2Stream.Properties;
            }

            return BuildPackedStream(
                isLzma2: true,
                properties,
                (ulong)(outputStream.Position - outStartOffset),
                (ulong)inputSize2,
                inputCrc2,
                outCrcStream.Crc
            );
        }

        // LZMA
        uint inputCrc;
        long inputSize;
        {
#if LEGACY_DOTNET
            using var lzmaStream = LzmaStream.Create(encoderProperties, false, outCrcStream);
#else
            await using var lzmaStream = LzmaStream.Create(encoderProperties, false, outCrcStream);
#endif
            properties = lzmaStream.Properties;

            (inputCrc, inputSize) = await CopyWithCrcAsync(
                    inputStream,
                    lzmaStream,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return BuildPackedStream(
            isLzma2: false,
            properties,
            (ulong)(outputStream.Position - outStartOffset),
            (ulong)inputSize,
            inputCrc,
            outCrcStream.Crc
        );
    }

    /// <summary>
    /// Copies data from source to destination while computing CRC32 of the source data.
    /// Uses Crc32Stream.Compute for CRC calculation to avoid duplicating the table/algorithm.
    /// </summary>
    private static void CopyWithCrc(
        Stream source,
        Stream destination,
        out uint crc,
        out long bytesRead
    )
    {
        var seed = Crc32Stream.DEFAULT_SEED;
        var buffer = new byte[81920];
        long totalRead = 0;

        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Crc32Stream.Compute returns ~CalculateCrc(table, seed, data),
            // so passing ~result as next seed chains correctly.
            seed = ~Crc32Stream.Compute(
                Crc32Stream.DEFAULT_POLYNOMIAL,
                seed,
                buffer.AsSpan(0, read)
            );
            destination.Write(buffer, 0, read);
            totalRead += read;
        }

        crc = ~seed;
        bytesRead = totalRead;
    }

    /// <summary>
    /// Asynchronously copies data from source to destination while computing CRC32 of source data.
    /// Uses Crc32Stream.Compute for CRC calculation to avoid duplicating the table/algorithm.
    /// </summary>
    private static async ValueTask<(uint crc, long bytesRead)> CopyWithCrcAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken
    )
    {
        var seed = Crc32Stream.DEFAULT_SEED;
        var buffer = new byte[81920];
        long totalRead = 0;

        int read;
        while (
            (
                read = await source
                    .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false)
            ) > 0
        )
        {
            // Crc32Stream.Compute returns ~CalculateCrc(table, seed, data),
            // so passing ~result as next seed chains correctly.
            seed = ~Crc32Stream.Compute(
                Crc32Stream.DEFAULT_POLYNOMIAL,
                seed,
                buffer.AsSpan(0, read)
            );
            await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            totalRead += read;
        }

        return (~seed, totalRead);
    }

    private static PackedStream BuildPackedStream(
        bool isLzma2,
        byte[] properties,
        ulong compressedSize,
        ulong uncompressedSize,
        uint inputCrc,
        uint? outputCrc
    )
    {
        var methodId = isLzma2 ? CMethodId.K_LZMA2 : CMethodId.K_LZMA;

        var folder = new CFolder();
        folder._coders.Add(
            new CCoderInfo
            {
                _methodId = methodId,
                _numInStreams = 1,
                _numOutStreams = 1,
                _props = properties,
            }
        );
        folder._packStreams.Add(0);
        folder._unpackSizes.Add((long)uncompressedSize);
        folder._unpackCrc = inputCrc;

        return new PackedStream
        {
            Folder = folder,
            Sizes = [compressedSize],
            CRCs = [outputCrc],
        };
    }
}
