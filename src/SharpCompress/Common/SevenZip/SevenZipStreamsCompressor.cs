using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Crypto;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Result of compressing one 7z folder.
/// </summary>
internal sealed class PackedFolder
{
    public CFolder Folder { get; init; } = new();
    public ulong PackSize { get; init; }
    public uint? PackCrc { get; init; }
    public ulong[] UnPackSizes { get; init; } = [];
    public uint?[] FileCrcs { get; init; } = [];
}

/// <summary>
/// Compresses one or more consecutive files into a single 7z folder.
/// </summary>
internal sealed class SevenZipFolderCompressor : IDisposable
{
    private readonly Stream outputStream;
    private readonly long outStartOffset;
    private readonly Crc32Stream packedCrcStream;
    private readonly Stream compressionStream;
    private readonly bool isLzma2;
    private readonly byte[] properties;
    private readonly List<ulong> unpackSizes = [];
    private readonly List<uint?> fileCrcs = [];
    private bool finalized;

    public SevenZipFolderCompressor(
        Stream outputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null
    )
    {
        if (compressionType != CompressionType.LZMA && compressionType != CompressionType.LZMA2)
        {
            throw new ArgumentException(
                $"SevenZipWriter only supports CompressionType.LZMA and CompressionType.LZMA2. Got: {compressionType}",
                nameof(compressionType)
            );
        }

        this.outputStream = outputStream;
        isLzma2 = compressionType == CompressionType.LZMA2;
        encoderProperties ??= new LzmaEncoderProperties(eos: !isLzma2);

        outStartOffset = outputStream.Position;
        packedCrcStream = new Crc32Stream(outputStream);

        if (isLzma2)
        {
            var lzma2Stream = new Lzma2EncoderStream(
                packedCrcStream,
                encoderProperties.DictionarySize,
                encoderProperties.NumFastBytes
            );
            compressionStream = lzma2Stream;
            properties = lzma2Stream.Properties;
        }
        else
        {
            var lzmaStream = LzmaStream.Create(encoderProperties, false, packedCrcStream);
            compressionStream = lzmaStream;
            properties = lzmaStream.Properties;
        }
    }

    public void Append(Stream inputStream, byte firstByte)
    {
        ThrowIfFinalized();

        CopyWithCrc(inputStream, compressionStream, firstByte, out var inputCrc, out var inputSize);
        unpackSizes.Add((ulong)inputSize);
        fileCrcs.Add(inputCrc);
    }

    public async ValueTask AppendAsync(
        Stream inputStream,
        byte firstByte,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfFinalized();
        cancellationToken.ThrowIfCancellationRequested();

        var (inputCrc, inputSize) = await CopyWithCrcAsync(
                inputStream,
                compressionStream,
                firstByte,
                cancellationToken
            )
            .ConfigureAwait(false);
        unpackSizes.Add((ulong)inputSize);
        fileCrcs.Add(inputCrc);
    }

    public PackedFolder FinalizeFolder()
    {
        ThrowIfFinalized();
        finalized = true;

        compressionStream.Dispose();
        packedCrcStream.Dispose();

        return BuildPackedFolder(
            isLzma2,
            properties,
            (ulong)(outputStream.Position - outStartOffset),
            packedCrcStream.Crc,
            unpackSizes.ToArray(),
            fileCrcs.ToArray()
        );
    }

    public void Dispose()
    {
        if (finalized)
        {
            return;
        }

        finalized = true;
        compressionStream.Dispose();
        packedCrcStream.Dispose();
    }

    private void ThrowIfFinalized()
    {
        if (finalized)
        {
            throw new ObjectDisposedException(nameof(SevenZipFolderCompressor));
        }
    }

    private static void CopyWithCrc(
        Stream source,
        Stream destination,
        byte firstByte,
        out uint crc,
        out long bytesRead
    )
    {
        var seed = Crc32Stream.DEFAULT_SEED;
        var buffer = new byte[81920];
        long totalRead = 1;

        buffer[0] = firstByte;
        seed = ~Crc32Stream.Compute(Crc32Stream.DEFAULT_POLYNOMIAL, seed, buffer.AsSpan(0, 1));
        destination.Write(buffer, 0, 1);

        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
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

    private static async ValueTask<(uint crc, long bytesRead)> CopyWithCrcAsync(
        Stream source,
        Stream destination,
        byte firstByte,
        CancellationToken cancellationToken
    )
    {
        var seed = Crc32Stream.DEFAULT_SEED;
        var buffer = new byte[81920];
        long totalRead = 1;

        buffer[0] = firstByte;
        seed = ~Crc32Stream.Compute(Crc32Stream.DEFAULT_POLYNOMIAL, seed, buffer.AsSpan(0, 1));
        await destination.WriteAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);

        int read;
        while (
            (
                read = await source
                    .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false)
            ) > 0
        )
        {
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

    private static PackedFolder BuildPackedFolder(
        bool isLzma2,
        byte[] properties,
        ulong compressedSize,
        uint? packedCrc,
        ulong[] uncompressedSizes,
        uint?[] fileCrcs
    )
    {
        var methodId = isLzma2 ? CMethodId.K_LZMA2 : CMethodId.K_LZMA;
        ulong totalUncompressedSize = 0;
        for (var i = 0; i < uncompressedSizes.Length; i++)
        {
            totalUncompressedSize += uncompressedSizes[i];
        }

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
        folder._unpackSizes.Add((long)totalUncompressedSize);

        return new PackedFolder
        {
            Folder = folder,
            PackSize = compressedSize,
            PackCrc = packedCrc,
            UnPackSizes = uncompressedSizes,
            FileCrcs = fileCrcs,
        };
    }
}

/// <summary>
/// Compresses input streams using LZMA or LZMA2 and writes the resulting folder data.
/// </summary>
internal sealed class SevenZipStreamsCompressor(Stream outputStream)
{
    public PackedFolder Compress(
        Stream inputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null
    )
    {
        var firstByte = inputStream.ReadByte();
        if (firstByte < 0)
        {
            throw new InvalidOperationException("Cannot compress an empty stream.");
        }

        using var folderCompressor = new SevenZipFolderCompressor(
            outputStream,
            compressionType,
            encoderProperties
        );
        folderCompressor.Append(inputStream, (byte)firstByte);
        return folderCompressor.FinalizeFolder();
    }

    public async ValueTask<PackedFolder> CompressAsync(
        Stream inputStream,
        CompressionType compressionType,
        LzmaEncoderProperties? encoderProperties = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var buffer = new byte[1];
        var bytesRead = await inputStream
            .ReadAsync(buffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead == 0)
        {
            throw new InvalidOperationException("Cannot compress an empty stream.");
        }

        using var folderCompressor = new SevenZipFolderCompressor(
            outputStream,
            compressionType,
            encoderProperties
        );
        await folderCompressor
            .AppendAsync(inputStream, buffer[0], cancellationToken)
            .ConfigureAwait(false);
        return folderCompressor.FinalizeFolder();
    }
}
