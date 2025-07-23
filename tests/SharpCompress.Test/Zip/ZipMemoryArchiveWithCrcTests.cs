using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Xz;
using SharpCompress.Crypto;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipTypesLevelsWithCrcRatioTests : ArchiveTests
{
    public ZipTypesLevelsWithCrcRatioTests() => UseExtensionInsteadOfNameToVerify = true;

    [Theory]
    [InlineData(CompressionType.Deflate, 1, 1, 0.11f)] // was 0.8f, actual 0.104
    [InlineData(CompressionType.Deflate, 3, 1, 0.08f)] // was 0.8f, actual 0.078
    [InlineData(CompressionType.Deflate, 6, 1, 0.05f)] // was 0.8f, actual ~0.042
    [InlineData(CompressionType.Deflate, 9, 1, 0.04f)] // was 0.7f, actual 0.038
    [InlineData(CompressionType.ZStandard, 1, 1, 0.025f)] // was 0.8f, actual 0.023
    [InlineData(CompressionType.ZStandard, 3, 1, 0.015f)] // was 0.7f, actual 0.013
    [InlineData(CompressionType.ZStandard, 9, 1, 0.006f)] // was 0.7f, actual 0.005
    [InlineData(CompressionType.ZStandard, 22, 1, 0.005f)] // was 0.7f, actual 0.004
    [InlineData(CompressionType.BZip2, 0, 1, 0.035f)] // was 0.8f, actual 0.033
    [InlineData(CompressionType.LZMA, 0, 1, 0.005f)] // was 0.8f, actual 0.004
    [InlineData(CompressionType.None, 0, 1, 1.001f)] // was 1.1f, actual 1.000
    [InlineData(CompressionType.Deflate, 6, 2, 0.045f)] // was 0.8f, actual 0.042
    [InlineData(CompressionType.ZStandard, 3, 2, 0.012f)] // was 0.7f, actual 0.010
    [InlineData(CompressionType.BZip2, 0, 2, 0.035f)] // was 0.8f, actual 0.032
    [InlineData(CompressionType.Deflate, 9, 3, 0.04f)] // was 0.7f, actual 0.038
    [InlineData(CompressionType.ZStandard, 9, 3, 0.003f)] // was 0.7f, actual 0.002
    public void Zip_Create_Archive_With_3_Files_Crc32_Test(
        CompressionType compressionType,
        int compressionLevel,
        int sizeMb,
        float expectedRatio
    )
    {
        const int OneMiB = 1024 * 1024;
        var baseSize = sizeMb * OneMiB;

        // Generate test content for files with sizes based on the sizeMb parameter
        var file1Data = TestPseudoTextStream.Create(baseSize);
        var file2Data = TestPseudoTextStream.Create(baseSize * 2);
        var file3Data = TestPseudoTextStream.Create(baseSize * 3);

        var expectedFiles = new Dictionary<string, (byte[] data, uint crc)>
        {
            [$"file1_{sizeMb}MiB.txt"] = (file1Data, CalculateCrc32(file1Data)),
            [$"data/file2_{sizeMb * 2}MiB.txt"] = (file2Data, CalculateCrc32(file2Data)),
            [$"deep/nested/file3_{sizeMb * 3}MiB.txt"] = (file3Data, CalculateCrc32(file3Data)),
        };

        // Create zip archive in memory
        using var zipStream = new MemoryStream();
        using (var writer = CreateWriterWithLevel(zipStream, compressionType, compressionLevel))
        {
            writer.Write($"file1_{sizeMb}MiB.txt", new MemoryStream(file1Data));
            writer.Write($"data/file2_{sizeMb * 2}MiB.txt", new MemoryStream(file2Data));
            writer.Write($"deep/nested/file3_{sizeMb * 3}MiB.txt", new MemoryStream(file3Data));
        }

        // Calculate and output actual compression ratio
        var originalSize = file1Data.Length + file2Data.Length + file3Data.Length;
        var actualRatio = (double)zipStream.Length / originalSize;
        //Debug.WriteLine($"Zip_Create_Archive_With_3_Files_Crc32_Test: {compressionType} Level={compressionLevel} Size={sizeMb}MB Expected={expectedRatio:F3} Actual={actualRatio:F3}");

        // Verify compression occurred (except for None compression type)
        if (compressionType != CompressionType.None)
        {
            Assert.True(
                zipStream.Length < originalSize,
                $"Compression failed: compressed={zipStream.Length}, original={originalSize}"
            );
        }

        // Verify compression ratio
        VerifyCompressionRatio(
            originalSize,
            zipStream.Length,
            expectedRatio,
            $"{compressionType} level {compressionLevel}"
        );

        // Verify archive content and CRC32
        VerifyArchiveContent(zipStream, expectedFiles);

        // Verify compression type is correctly set
        VerifyCompressionType(zipStream, compressionType);
    }

    [Theory]
    [InlineData(CompressionType.Deflate, 1, 4, 0.11f)] // was 0.8, actual 0.105
    [InlineData(CompressionType.Deflate, 3, 4, 0.08f)] // was 0.8, actual 0.077
    [InlineData(CompressionType.Deflate, 6, 4, 0.045f)] // was 0.8, actual 0.042
    [InlineData(CompressionType.Deflate, 9, 4, 0.04f)] // was 0.8, actual 0.037
    [InlineData(CompressionType.ZStandard, 1, 4, 0.025f)] // was 0.8, actual 0.022
    [InlineData(CompressionType.ZStandard, 3, 4, 0.012f)] // was 0.8, actual 0.010
    [InlineData(CompressionType.ZStandard, 9, 4, 0.003f)] // was 0.8, actual 0.002
    [InlineData(CompressionType.ZStandard, 22, 4, 0.003f)] // was 0.8, actual 0.002
    [InlineData(CompressionType.BZip2, 0, 4, 0.035f)] // was 0.8, actual 0.032
    [InlineData(CompressionType.LZMA, 0, 4, 0.003f)] // was 0.8, actual 0.002
    public void Zip_WriterFactory_Crc32_Test(
        CompressionType compressionType,
        int compressionLevel,
        int sizeMb,
        float expectedRatio
    )
    {
        var fileSize = sizeMb * 1024 * 1024;

        var testData = TestPseudoTextStream.Create(fileSize);
        var expectedCrc = CalculateCrc32(testData);

        // Create archive with specified compression level
        using var zipStream = new MemoryStream();
        var writerOptions = new ZipWriterOptions(compressionType)
        {
            CompressionLevel = compressionLevel,
        };

        using (var writer = WriterFactory.Open(zipStream, ArchiveType.Zip, writerOptions))
        {
            writer.Write(
                $"{compressionType}_level_{compressionLevel}_{sizeMb}MiB.txt",
                new MemoryStream(testData)
            );
        }

        // Calculate and output actual compression ratio
        var actualRatio = (double)zipStream.Length / testData.Length;
        //Debug.WriteLine($"Zip_WriterFactory_Crc32_Test: {compressionType} Level={compressionLevel} Size={sizeMb}MB Expected={expectedRatio:F3} Actual={actualRatio:F3}");

        VerifyCompressionRatio(
            testData.Length,
            zipStream.Length,
            expectedRatio,
            $"{compressionType} level {compressionLevel}"
        );

        // Verify the archive
        zipStream.Position = 0;
        using var archive = ZipArchive.Open(zipStream);

        var entry = archive.Entries.Single(e => !e.IsDirectory);
        using var entryStream = entry.OpenEntryStream();
        using var extractedStream = new MemoryStream();
        entryStream.CopyTo(extractedStream);

        var extractedData = extractedStream.ToArray();
        var actualCrc = CalculateCrc32(extractedData);

        Assert.Equal(compressionType, entry.CompressionType);
        Assert.Equal(expectedCrc, actualCrc);
        Assert.Equal(testData.Length, extractedData.Length);
        Assert.Equal(testData, extractedData);
    }

    [Theory]
    [InlineData(CompressionType.Deflate, 1, 2, 0.11f)] // was 0.8, actual 0.104
    [InlineData(CompressionType.Deflate, 3, 2, 0.08f)] // was 0.8, actual 0.077
    [InlineData(CompressionType.Deflate, 6, 2, 0.045f)] // was 0.8, actual 0.042
    [InlineData(CompressionType.Deflate, 9, 2, 0.04f)] // was 0.7, actual 0.038
    [InlineData(CompressionType.ZStandard, 1, 2, 0.025f)] // was 0.8, actual 0.023
    [InlineData(CompressionType.ZStandard, 3, 2, 0.015f)] // was 0.7, actual 0.012
    [InlineData(CompressionType.ZStandard, 9, 2, 0.006f)] // was 0.7, actual 0.005
    [InlineData(CompressionType.ZStandard, 22, 2, 0.005f)] // was 0.7, actual 0.004
    [InlineData(CompressionType.BZip2, 0, 2, 0.035f)] // was 0.8, actual 0.032
    [InlineData(CompressionType.LZMA, 0, 2, 0.005f)] // was 0.8, actual 0.004
    public void Zip_ZipArchiveOpen_Crc32_Test(
        CompressionType compressionType,
        int compressionLevel,
        int sizeMb,
        float expectedRatio
    )
    {
        var fileSize = sizeMb * 1024 * 1024;

        var testData = TestPseudoTextStream.Create(fileSize);
        var expectedCrc = CalculateCrc32(testData);

        // Create archive with specified compression and level
        using var zipStream = new MemoryStream();
        using (var writer = CreateWriterWithLevel(zipStream, compressionType, compressionLevel))
        {
            writer.Write(
                $"{compressionType}_{compressionLevel}_{sizeMb}MiB.txt",
                new MemoryStream(testData)
            );
        }

        // Calculate and output actual compression ratio
        var actualRatio = (double)zipStream.Length / testData.Length;
        //Debug.WriteLine($"Zip_ZipArchiveOpen_Crc32_Test: {compressionType} Level={compressionLevel} Size={sizeMb}MB Expected={expectedRatio:F3} Actual={actualRatio:F3}");

        // Verify the archive
        zipStream.Position = 0;
        using var archive = ZipArchive.Open(zipStream);

        var entry = archive.Entries.Single(e => !e.IsDirectory);
        using var entryStream = entry.OpenEntryStream();
        using var extractedStream = new MemoryStream();
        entryStream.CopyTo(extractedStream);

        var extractedData = extractedStream.ToArray();
        var actualCrc = CalculateCrc32(extractedData);

        Assert.Equal(compressionType, entry.CompressionType);
        Assert.Equal(expectedCrc, actualCrc);
        Assert.Equal(testData.Length, extractedData.Length);

        // For smaller files, verify full content; for larger, spot check
        if (testData.Length <= sizeMb * 2)
        {
            Assert.Equal(testData, extractedData);
        }
        else
        {
            VerifyDataSpotCheck(testData, extractedData);
        }

        VerifyCompressionRatio(
            testData.Length,
            zipStream.Length,
            expectedRatio,
            $"{compressionType} Level {compressionLevel}"
        );
    }
}
