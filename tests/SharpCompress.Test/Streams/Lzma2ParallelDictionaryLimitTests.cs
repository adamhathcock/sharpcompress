using System;
using System.IO;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Readers;
using SharpCompress.Writers.SevenZip;
using Xunit;

namespace SharpCompress.Test.Streams;

[Collection(DictionarySizeLimitCollection.Name)]
public class Lzma2ParallelDictionaryLimitTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SevenZip_File_Extraction_Reports_Dictionary_Limits_Consistently(bool parallel)
    {
        using var limits = new DictionarySizeLimitScope();
        var path = Path.Combine(SCRATCH_FILES_PATH, "parallel-dictionary-limit.7z");
        using (var output = File.Create(path))
        using (
            var writer = new SevenZipWriter(
                output,
                new SevenZipWriterOptions(CompressionType.LZMA2)
                {
                    CompressHeader = false,
                    LzmaProperties = new LzmaEncoderProperties(false, 8192, 32),
                }
            )
        )
        {
            // Two full-reset chunks make the file-backed parallel path eligible.
            using var source = new MemoryStream(new byte[(2 << 20) + (1 << 16)]);
            writer.Write("payload.bin", source, DateTime.UtcNow);
        }
        DictionarySizeLimits.Lzma2 = 4096;
        using var archive = SevenZipArchive.OpenArchive(
            path,
            ReaderOptions.ForFilePath.WithEnableParallelism(parallel)
        );
        var entry = Assert.Single(archive.Entries);
        Assert.Throws<InvalidFormatException>(() =>
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        });
    }

#if !LEGACY_DOTNET
    [Fact]
    public void Parallel_Dictionary_Validation_Runs_Before_Workers()
    {
        using var limits = new DictionarySizeLimitScope();
        DictionarySizeLimits.Lzma2 = 4096;
        var path = Path.Combine(SCRATCH_FILES_PATH, "parallel-dictionary-workers.bin");
        using var input = File.Create(path);
        using var output = File.Create(path + ".out");
        var blocks = new[] { new Lzma2Block(0, 1, 0, 1), new Lzma2Block(1, 1, 1, 1) };
        Assert.Throws<InvalidFormatException>(() =>
            Lzma2ParallelDecoder.DecodeBlocksParallel(
                input.SafeFileHandle,
                new byte[] { 2 },
                0,
                blocks,
                output.SafeFileHandle,
                2
            )
        );
        Assert.Equal(0, output.Length);
    }
#endif
}
