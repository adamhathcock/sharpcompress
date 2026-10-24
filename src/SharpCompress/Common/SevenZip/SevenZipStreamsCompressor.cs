using System;
using System.Buffers;
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
/// Uses SharpCompress's existing LzmaStream encoder. With <see cref="SevenZipAesEncryption" />,
/// the compressed data is also encrypted (LZMA/LZMA2 -> 7zAES), as 7-Zip does.
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
    /// <param name="encryption">7zAES settings, or null to store the data unencrypted.</param>
    public PackedStream Compress(
        Stream inputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null,
        SevenZipAesEncryption? encryption = null
    )
    {
        var isLzma2 = compressionType == CompressionType.LZMA2;
        encoderProperties ??= new LzmaEncoderProperties(eos: !isLzma2);

        var outStartOffset = outputStream.Position;

        // Wrap the output stream in CRC calculator
        using var outCrcStream = new Crc32Stream(outputStream);

        // The packed stream is the encrypted data; the codec writes into the encoder.
        byte[]? aesProperties = null;
        using var aesStream = encryption?.CreateEncoder(outCrcStream, out aesProperties);
        Stream codecOutput = aesStream ?? (Stream)outCrcStream;

        byte[] properties;

        if (isLzma2)
        {
            // LZMA2: use Lzma2EncoderStream for chunk-based framing
            using var lzma2Stream = new Lzma2EncoderStream(
                codecOutput,
                encoderProperties.DictionarySize,
                encoderProperties.NumFastBytes
            );

            CopyWithCrc(inputStream, lzma2Stream, out var inputCrc2, out var inputSize2);
            lzma2Stream.Dispose();
            aesStream?.Finish();

            properties = lzma2Stream.Properties;

            return BuildPackedStream(
                isLzma2: true,
                properties,
                (ulong)(outputStream.Position - outStartOffset),
                (ulong)inputSize2,
                inputCrc2,
                outCrcStream.Crc,
                aesProperties,
                aesStream?.InputLength
            );
        }

        // LZMA
        using var lzmaStream = LzmaStream.Create(encoderProperties, false, codecOutput);
        properties = lzmaStream.Properties;

        CopyWithCrc(inputStream, lzmaStream, out var inputCrc, out var inputSize);
        lzmaStream.Dispose();
        aesStream?.Finish();

        return BuildPackedStream(
            isLzma2: false,
            properties,
            (ulong)(outputStream.Position - outStartOffset),
            (ulong)inputSize,
            inputCrc,
            outCrcStream.Crc,
            aesProperties,
            aesStream?.InputLength
        );
    }

    /// <summary>
    /// Asynchronously compresses the input stream to the output stream using the specified method.
    /// Returns a PackedStream containing folder metadata, compressed size, and CRCs.
    /// </summary>
    /// <param name="inputStream">Uncompressed data to compress.</param>
    /// <param name="compressionType">Compression method (LZMA or LZMA2).</param>
    /// <param name="encoderProperties">LZMA encoder properties (null for defaults).</param>
    /// <param name="encryption">7zAES settings, or null to store the data unencrypted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<PackedStream> CompressAsync(
        Stream inputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null,
        SevenZipAesEncryption? encryption = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isLzma2 = compressionType == CompressionType.LZMA2;
        encoderProperties ??= new LzmaEncoderProperties(eos: !isLzma2);

        var outStartOffset = outputStream.Position;

        // Wrap the output stream in CRC calculator
        using var outCrcStream = new Crc32Stream(outputStream);

        // The packed stream is the encrypted data; the codec writes into the encoder.
        byte[]? aesProperties = null;
        using var aesStream = encryption?.CreateEncoder(outCrcStream, out aesProperties);
        Stream codecOutput = aesStream ?? (Stream)outCrcStream;

        byte[] properties;

        if (isLzma2)
        {
            // LZMA2: use Lzma2EncoderStream for chunk-based framing
            uint inputCrc2;
            long inputSize2;
            {
                await using var lzma2Stream = new Lzma2EncoderStream(
                    codecOutput,
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

            if (aesStream is not null)
            {
                await aesStream.FinishAsync(cancellationToken).ConfigureAwait(false);
            }

            return BuildPackedStream(
                isLzma2: true,
                properties,
                (ulong)(outputStream.Position - outStartOffset),
                (ulong)inputSize2,
                inputCrc2,
                outCrcStream.Crc,
                aesProperties,
                aesStream?.InputLength
            );
        }

        // LZMA
        uint inputCrc;
        long inputSize;
        {
            await using var lzmaStream = LzmaStream.Create(encoderProperties, false, codecOutput);
            properties = lzmaStream.Properties;

            (inputCrc, inputSize) = await CopyWithCrcAsync(
                    inputStream,
                    lzmaStream,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (aesStream is not null)
        {
            await aesStream.FinishAsync(cancellationToken).ConfigureAwait(false);
        }

        return BuildPackedStream(
            isLzma2: false,
            properties,
            (ulong)(outputStream.Position - outStartOffset),
            (ulong)inputSize,
            inputCrc,
            outCrcStream.Crc,
            aesProperties,
            aesStream?.InputLength
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
        // Stay in the 64 KiB pool bucket rather than rounding an 80 KiB request to 128 KiB.
        const int bufferSize = 65536;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            long totalRead = 0;

            int read;
            while ((read = source.Read(buffer, 0, bufferSize)) > 0)
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
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
        const int bufferSize = 65536;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            long totalRead = 0;

            int read;
            while (
                (
                    read = await source
#if LEGACY_DOTNET
                        .ReadAsync(buffer, 0, bufferSize, cancellationToken)
#else
                        .ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)
#endif
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
                await destination
                    .WriteAsync(buffer, 0, read, cancellationToken)
                    .ConfigureAwait(false);
                totalRead += read;
            }

            return (~seed, totalRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static PackedStream BuildPackedStream(
        bool isLzma2,
        byte[] properties,
        ulong compressedSize,
        ulong uncompressedSize,
        uint inputCrc,
        uint? outputCrc,
        byte[]? aesProperties,
        long? codecOutputSize
    )
    {
        var methodId = isLzma2 ? CMethodId.K_LZMA2 : CMethodId.K_LZMA;

        var folder = new CFolder();
        var codec = new CCoderInfo
        {
            _methodId = methodId,
            _numInStreams = 1,
            _numOutStreams = 1,
            _props = properties,
        };

        if (aesProperties is null)
        {
            folder._coders.Add(codec);
            folder._packStreams.Add(0);
            folder._unpackSizes.Add((long)uncompressedSize);
        }
        else
        {
            // Same layout as 7-Zip: coder 0 is 7zAES and reads the packed stream (in-stream 0);
            // coder 1 is LZMA/LZMA2, reads coder 0's output (in-stream 1 <- out-stream 0) and
            // produces the file data. Unpack sizes are per out-stream, in coder order; the AES
            // size excludes the CBC padding.
            folder._coders.Add(
                new CCoderInfo
                {
                    _methodId = CMethodId.K_AES,
                    _numInStreams = 1,
                    _numOutStreams = 1,
                    _props = aesProperties,
                }
            );
            folder._coders.Add(codec);
            folder._bindPairs.Add(new CBindPair { _inIndex = 1, _outIndex = 0 });
            folder._packStreams.Add(0);
            folder._unpackSizes.Add(codecOutputSize.NotNull());
            folder._unpackSizes.Add((long)uncompressedSize);
        }

        folder._unpackCrc = inputCrc;

        return new PackedStream
        {
            Folder = folder,
            Sizes = [compressedSize],
            CRCs = [outputCrc],
        };
    }
}
