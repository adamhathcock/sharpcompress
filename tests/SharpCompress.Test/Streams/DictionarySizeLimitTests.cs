using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.PPMd;
using SharpCompress.Compressors.PPMd.H;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.Compressors.ZStandard.Unsafe;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Streams;

// Static settings must not change while other collections are extracting archives.
[CollectionDefinition(Name, DisableParallelization = true)]
public class DictionarySizeLimitCollection
{
    public const string Name = "Dictionary size limits";
}

internal sealed class DictionarySizeLimitScope : IDisposable
{
    private readonly long rar4 = DictionarySizeLimits.Rar4;
    private readonly long rar5 = DictionarySizeLimits.Rar5;
    private readonly long lzma = DictionarySizeLimits.Lzma;
    private readonly long lzma2 = DictionarySizeLimits.Lzma2;
    private readonly long ppmd = DictionarySizeLimits.Ppmd;
    private readonly long zStandard = DictionarySizeLimits.ZStandard;

    public void Dispose()
    {
        DictionarySizeLimits.Rar4 = rar4;
        DictionarySizeLimits.Rar5 = rar5;
        DictionarySizeLimits.Lzma = lzma;
        DictionarySizeLimits.Lzma2 = lzma2;
        DictionarySizeLimits.Ppmd = ppmd;
        DictionarySizeLimits.ZStandard = zStandard;
    }
}

[Collection(DictionarySizeLimitCollection.Name)]
public class DictionarySizeLimitTests : TestBase
{
    [Fact]
    public void Static_Limits_Have_Format_Defaults_And_Reject_Nonpositive_Values()
    {
        using var limits = new DictionarySizeLimitScope();
        Assert.Equal(4L << 20, DictionarySizeLimits.Rar4);
        Assert.Equal(64L << 20, DictionarySizeLimits.Rar5);
        Assert.Equal(64L << 20, DictionarySizeLimits.Lzma);
        Assert.Equal(64L << 20, DictionarySizeLimits.Lzma2);
        Assert.Equal(64L << 20, DictionarySizeLimits.Ppmd);
        Assert.Equal(128L << 20, DictionarySizeLimits.ZStandard);
        DictionarySizeLimits.Rar5 = 256L << 20;
        Assert.Equal(256L << 20, DictionarySizeLimits.Rar5);
        Assert.Throws<ArgumentOutOfRangeException>(() => DictionarySizeLimits.Rar4 = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => DictionarySizeLimits.Rar5 = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => DictionarySizeLimits.Lzma = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => DictionarySizeLimits.Lzma2 = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => DictionarySizeLimits.Ppmd = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => DictionarySizeLimits.ZStandard = -1);
        Assert.Equal(256L << 20, DictionarySizeLimits.Rar5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma_And_Lzma2_Reject_Excessive_Declared_Dictionaries(bool useAsync)
    {
        foreach (var properties in new[] { new byte[] { 0x5d, 0, 0, 0, 0x10 }, new byte[] { 30 } })
        {
            using var input = new MemoryStream();
            if (useAsync)
            {
                await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                    await LzmaStream.CreateAsync(properties, input, -1, -1)
                );
            }
            else
            {
                Assert.Throws<InvalidFormatException>(() => LzmaStream.Create(properties, input));
            }
            Assert.True(input.CanRead);
            Assert.Equal(0, input.Position);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_Lzma_Decoder_Enforces_Default_Limit(bool useAsync)
    {
        using var decoder = new Decoder();
        decoder.SetDecoderProperties(new byte[] { 0x5d, 0, 0, 0, 0x10 });
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await decoder.CodeAsync(
                    Stream.Null,
                    Stream.Null,
                    0,
                    1,
                    null!,
                    CancellationToken.None
                )
            );
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() =>
                decoder.Code(Stream.Null, Stream.Null, 0, 1, null!)
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma2_Static_Limit_Is_Inclusive(bool useAsync)
    {
        using var limits = new DictionarySizeLimitScope();
        using var input = new MemoryStream(new byte[] { 1, 0, 0, 65, 0 });
        DictionarySizeLimits.Lzma2 = 4095;
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await LzmaStream.CreateAsync(new byte[] { 0 }, input, -1, -1)
            );
            DictionarySizeLimits.Lzma2 = 4096;
            using var decoder = await LzmaStream.CreateAsync(new byte[] { 0 }, input, -1, -1);
            var buffer = new byte[1];
            Assert.Equal(1, await decoder.ReadAsync(buffer, 0, 1, CancellationToken.None));
            Assert.Equal(65, buffer[0]);
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() => LzmaStream.Create(new byte[] { 0 }, input));
            DictionarySizeLimits.Lzma2 = 4096;
            using var decoder = LzmaStream.Create(new byte[] { 0 }, input);
            Assert.Equal(65, decoder.ReadByte());
        }
    }

    [Fact]
    public void Ppmd_Properties_Reject_Large_Models_Before_Eager_Allocation()
    {
        Assert.Throws<InvalidFormatException>(() => new PpmdProperties(new byte[] { 0xf5, 0x0f }));
        Assert.Throws<InvalidFormatException>(() => new PpmdProperties(256 << 20, 6));
    }

    [Fact]
    public void Ppmd_Properties_Preserve_Size_After_Rejected_Change()
    {
        using var limits = new DictionarySizeLimitScope();
        DictionarySizeLimits.Ppmd = 2 << 20;
        var properties = new PpmdProperties(1 << 20, 6);
        var originalHeader = properties.Properties;

        Assert.Throws<InvalidFormatException>(() => properties.AllocatorSize = 3 << 20);
        Assert.Equal(1 << 20, properties.AllocatorSize);
        Assert.Equal(originalHeader, properties.Properties);

        properties.AllocatorSize = 2 << 20;
        Assert.Equal(2 << 20, properties.AllocatorSize);
        Assert.Equal(2 << 20, new PpmdProperties(properties.Properties).AllocatorSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZip_Ppmd_Rejects_Large_Models_Before_Allocation(bool useAsync)
    {
        var properties = new PpmdProperties(new byte[] { 6, 0, 0, 0, 0x10 });
        using var input = new MemoryStream();
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await PpmdStream.CreateAsync(properties, input, false, CancellationToken.None)
            );
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() =>
                PpmdStream.Create(properties, input, false)
            );
        }
        Assert.True(input.CanRead);
    }

    [Fact]
    public void Rar_Ppmd_Suballocator_Enforces_Limit_Before_Renting()
    {
        using var limits = new DictionarySizeLimitScope();
        DictionarySizeLimits.Ppmd = 1 << 20;
        using var allocator = new SubAllocator();
        Assert.Throws<InvalidFormatException>(() => allocator.StartSubAllocator(2 << 20));
        Assert.Equal(0, allocator.GetAllocatedMemory());
        Assert.True(allocator.StartSubAllocator(1 << 20));
        Assert.Equal(1 << 20, allocator.GetAllocatedMemory());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LZip_Uses_Static_Lzma_Limit(bool useAsync)
    {
        var bytes = new byte[26];
        new byte[] { (byte)'L', (byte)'Z', (byte)'I', (byte)'P', 1, 28 }.CopyTo(bytes, 0);
        using var input = new MemoryStream(bytes);
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await LZipStream.CreateAsync(
                    input,
                    CompressionMode.Decompress,
                    cancellationToken: CancellationToken.None
                )
            );
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() =>
                LZipStream.Create(input, CompressionMode.Decompress)
            );
        }
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zstandard_Enforces_Default_And_Exact_Static_Window_Limits(bool useAsync)
    {
        using var limits = new DictionarySizeLimitScope();
        var defaultMaximum = DictionarySizeLimits.ZStandard;
        foreach (var maximum in new long?[] { null, 16383, 16384 })
        {
            DictionarySizeLimits.ZStandard = maximum ?? defaultMaximum;
            // A 1 GiB or 16 KiB window with a final empty raw block.
            using var input = new MemoryStream(
                new byte[]
                {
                    0x28,
                    0xb5,
                    0x2f,
                    0xfd,
                    0,
                    maximum is null ? (byte)0xa0 : (byte)0x20,
                    1,
                    0,
                    0,
                }
            );
            using var decoder = new DecompressionStream(input);
            var buffer = new byte[1];
            if (maximum == 16384)
            {
                var read = useAsync
                    ? await decoder.ReadAsync(buffer, 0, 1, CancellationToken.None)
                    : decoder.Read(buffer, 0, 1);
                Assert.Equal(0, read);
            }
            else
            {
                var exception = useAsync
                    ? await Assert.ThrowsAsync<ZstdException>(async () =>
                        await decoder.ReadAsync(buffer, 0, 1, CancellationToken.None)
                    )
                    : Assert.Throws<ZstdException>(() => decoder.Read(buffer, 0, 1));
                Assert.Equal(
                    ZSTD_ErrorCode.ZSTD_error_frameParameter_windowTooLarge,
                    exception.Code
                );
            }
        }
    }

    public static IEnumerable<object[]> ArchiveCases()
    {
        foreach (
            var file in new[]
            {
                "Rar.rar",
                "Rar4.rar",
                "Rar5.rar",
                "Rar5.solid.rar",
                "Zip.lzma.zip",
                "Zip.ppmd.zip",
                "WinZip27_XZ.zipx",
                "WinZip27_ZSTD.zipx",
                "7Zip.LZMA.7z",
                "7Zip.LZMA2.7z",
                "7Zip.PPMd.7z",
                "7Zip.ZSTD.7z",
                "Tar.tar.xz",
                "Tar.tar.lz",
                "Tar.tar.zst",
            }
        )
        {
            foreach (var archiveApi in new[] { false, true })
            {
                foreach (var useAsync in new[] { false, true })
                {
                    if (
                        (!archiveApi && file.EndsWith(".7z", StringComparison.Ordinal))
                        || (archiveApi && file.StartsWith("Tar.", StringComparison.Ordinal))
                    )
                    {
                        continue;
                    }
                    yield return new object[] { file, archiveApi, useAsync };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ArchiveCases))]
    public async Task Static_Limits_Apply_To_Archive_And_Reader_Paths(
        string file,
        bool archiveApi,
        bool useAsync
    )
    {
        using (var limits = new DictionarySizeLimitScope())
        {
            DictionarySizeLimits.Rar4 =
                DictionarySizeLimits.Rar5 =
                DictionarySizeLimits.Lzma =
                DictionarySizeLimits.Lzma2 =
                DictionarySizeLimits.Ppmd =
                DictionarySizeLimits.ZStandard =
                    1024;
            await Assert.ThrowsAnyAsync<SharpCompressException>(() =>
                Extract(file, archiveApi, useAsync)
            );
        }
        await Extract(file, archiveApi, useAsync);
    }

    private static async Task Extract(string file, bool archiveApi, bool useAsync)
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, file));
        var options = ReaderOptions.ForExternalStream;
        if (archiveApi && useAsync)
        {
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                input,
                options,
                CancellationToken.None
            );
            await foreach (var entry in archive.EntriesAsync)
            {
                if (!entry.IsDirectory)
                {
                    using var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                    await stream.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
                }
            }
        }
        else if (archiveApi)
        {
            using var archive = ArchiveFactory.OpenArchive(input, options);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                using var stream = entry.OpenEntryStream();
                stream.CopyTo(Stream.Null);
            }
        }
        else if (useAsync)
        {
            using var forwardOnly = new NonSeekableStream(input);
            await using var reader = await ReaderFactory.OpenAsyncReader(
                forwardOnly,
                options,
                CancellationToken.None
            );
            while (await reader.MoveToNextEntryAsync(CancellationToken.None))
            {
                if (!reader.Entry.IsDirectory)
                {
                    await reader.WriteEntryToAsync(Stream.Null, CancellationToken.None);
                }
            }
        }
        else
        {
            using var forwardOnly = new NonSeekableStream(input);
            using var reader = ReaderFactory.OpenReader(forwardOnly, options);
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryTo(Stream.Null);
                }
            }
        }
    }
}
