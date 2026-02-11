using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

/// <summary>
/// Tests specifically designed to exercise the Unpack5Async code path for RAR5 archives.
/// These tests use AsyncOnlyStream to ensure that async methods are actually being called.
/// </summary>
public class Rar5AsyncExtractionTests : ArchiveTests
{
    [Fact]
    public async Task Rar5_Basic_Reader_ExtractAll_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_None_Reader_ExtractAll_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.none.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Solid_ExtractAllEntries_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar"));
        await using var archive = RarArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));

        Assert.True(await archive.IsSolidAsync());

        await using var reader = await archive.ExtractAllEntriesAsync();
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Solid_Reader_ExtractAll_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Reader_OpenEntryStream_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                var entryStream = await reader.OpenEntryStreamAsync();
                try
                {
                    var file = Path.GetFileName(reader.Entry.Key).NotNull();
                    var folder = Path.GetDirectoryName(reader.Entry.Key) ?? "";
                    var destdir = Path.Combine(SCRATCH_FILES_PATH, folder);
                    if (!Directory.Exists(destdir))
                    {
                        Directory.CreateDirectory(destdir);
                    }
                    var destinationFileName = Path.Combine(destdir, file);

                    using var fs = File.OpenWrite(destinationFileName);
                    await entryStream.CopyToAsync(fs);
                }
                finally
                {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                    await entryStream.DisposeAsync();
#else
                    entryStream.Dispose();
#endif
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Encrypted_FilesOnly_Reader_Async()
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.encrypted_filesOnly.rar")
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            new ReaderOptions { Password = "test" }
        );

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Encrypted_FilesAndHeader_Reader_Async()
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.encrypted_filesAndHeader.rar")
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            new ReaderOptions { Password = "test" }
        );

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_CRC_Blake2_Reader_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.crc_blake2.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Comment_Reader_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.comment.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Solid_Skip_Some_Entries_Reader_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar"));
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));

        while (await reader.MoveToNextEntryAsync())
        {
            // Only extract jpg files to test skipping in solid archive
            if (!reader.Entry.IsDirectory && reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
    }

    [Fact]
    public async Task Rar5_WriteToDirectory_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.rar"));
        await using var archive = RarArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));

        await archive.WriteToDirectoryAsync(SCRATCH_FILES_PATH);
        VerifyFiles();
    }

    [Fact]
    public async Task Rar5_Solid_WriteToDirectory_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar"));
        await using var archive = RarArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));

        Assert.True(await archive.IsSolidAsync());
        await archive.WriteToDirectoryAsync(SCRATCH_FILES_PATH);
        VerifyFiles();
    }
}
