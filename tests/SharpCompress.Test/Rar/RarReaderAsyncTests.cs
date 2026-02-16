using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarReaderAsyncTests : ReaderTests
{
    [Fact]
    public async ValueTask Rar_Multi_Reader_Async() =>
        await DoRar_Multi_Reader_Async([
            "Rar.multi.part01.rar",
            "Rar.multi.part02.rar",
            "Rar.multi.part03.rar",
            "Rar.multi.part04.rar",
            "Rar.multi.part05.rar",
            "Rar.multi.part06.rar",
        ]);

    [Fact]
    public async ValueTask Rar5_Multi_Reader_Async() =>
        await DoRar_Multi_Reader_Async([
            "Rar5.multi.part01.rar",
            "Rar5.multi.part02.rar",
            "Rar5.multi.part03.rar",
            "Rar5.multi.part04.rar",
            "Rar5.multi.part05.rar",
            "Rar5.multi.part06.rar",
        ]);

    private async ValueTask DoRar_Multi_Reader_Async(string[] archives)
    {
        using (
            IReader baseReader = RarReader.OpenReader(
                archives
                    .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                    .Select(p => File.OpenRead(p))
            )
        )
        {
            IAsyncReader reader = (IAsyncReader)baseReader;
            while (await reader.MoveToNextEntryAsync())
            {
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async ValueTask Rar_Multi_Reader_Encrypted_Async() =>
        await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
        {
            string[] archives =
            [
                "Rar.EncryptedParts.part01.rar",
                "Rar.EncryptedParts.part02.rar",
                "Rar.EncryptedParts.part03.rar",
                "Rar.EncryptedParts.part04.rar",
                "Rar.EncryptedParts.part05.rar",
                "Rar.EncryptedParts.part06.rar",
            ];
            using (
                IReader baseReader = RarReader.OpenReader(
                    archives
                        .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                        .Select(p => File.OpenRead(p)),
                    new ReaderOptions { Password = "test" }
                )
            )
            {
                IAsyncReader reader = (IAsyncReader)baseReader;
                while (await reader.MoveToNextEntryAsync())
                {
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
                }
            }
            VerifyFiles();
        });

    [Fact]
    public async ValueTask Rar_Multi_Reader_Delete_Files_Async() =>
        await DoRar_Multi_Reader_Delete_Files_Async([
            "Rar.multi.part01.rar",
            "Rar.multi.part02.rar",
            "Rar.multi.part03.rar",
            "Rar.multi.part04.rar",
            "Rar.multi.part05.rar",
            "Rar.multi.part06.rar",
        ]);

    [Fact]
    public async ValueTask Rar5_Multi_Reader_Delete_Files_Async() =>
        await DoRar_Multi_Reader_Delete_Files_Async([
            "Rar5.multi.part01.rar",
            "Rar5.multi.part02.rar",
            "Rar5.multi.part03.rar",
            "Rar5.multi.part04.rar",
            "Rar5.multi.part05.rar",
            "Rar5.multi.part06.rar",
        ]);

    private async ValueTask DoRar_Multi_Reader_Delete_Files_Async(string[] archives)
    {
        foreach (var file in archives)
        {
            File.Copy(
                Path.Combine(TEST_ARCHIVES_PATH, file),
                Path.Combine(SCRATCH2_FILES_PATH, file)
            );
        }
        var streams = archives
            .Select(s => Path.Combine(SCRATCH2_FILES_PATH, s))
            .Select(File.OpenRead)
            .ToList();
        using (IReader baseReader = RarReader.OpenReader(streams))
        {
            IAsyncReader reader = (IAsyncReader)baseReader;
            while (await reader.MoveToNextEntryAsync())
            {
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        foreach (var stream in streams)
        {
            stream.Dispose();
        }
        VerifyFiles();

        foreach (var file in archives.Select(s => Path.Combine(SCRATCH2_FILES_PATH, s)))
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async ValueTask Rar_None_Reader_Async() =>
        await ReadAsync("Rar.none.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar5_None_Reader_Async() =>
        await ReadAsync("Rar5.none.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar_Reader_Async() => await ReadAsync("Rar.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar5_Reader_Async() => await ReadAsync("Rar5.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar5_CRC_Blake2_Reader_Async() =>
        await ReadAsync("Rar5.crc_blake2.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar_EncryptedFileAndHeader_Reader_Async() =>
        await ReadRar_Async("Rar.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public async ValueTask Rar5_EncryptedFileAndHeader_Reader_Async() =>
        await ReadRar_Async("Rar5.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public async ValueTask Rar_EncryptedFileOnly_Reader_Async() =>
        await ReadRar_Async("Rar.encrypted_filesOnly.rar", "test");

    [Fact]
    public async ValueTask Rar5_EncryptedFileOnly_Reader_Async() =>
        await ReadRar_Async("Rar5.encrypted_filesOnly.rar", "test");

    [Fact]
    public async ValueTask Rar_Encrypted_Reader_Async() =>
        await ReadRar_Async("Rar.Encrypted.rar", "test");

    [Fact]
    public async ValueTask Rar5_Encrypted_Reader_Async() =>
        await ReadRar_Async("Rar5.encrypted_filesOnly.rar", "test");

    private async ValueTask ReadRar_Async(string testArchive, string password) =>
        await ReadAsync(
            testArchive,
            CompressionType.Rar,
            new ReaderOptions { Password = password }
        );

    [Fact]
    public async ValueTask Rar_Entry_Stream_Async() => await DoRar_Entry_Stream_Async("Rar.rar");

    [Fact]
    public async ValueTask Rar5_Entry_Stream_Async() => await DoRar_Entry_Stream_Async("Rar5.rar");

    private async ValueTask DoRar_Entry_Stream_Async(string filename)
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
        await using (var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream)))
        {
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                    var entryStream = await reader.OpenEntryStreamAsync();
                    try
                    {
                        var file = Path.GetFileName(reader.Entry.Key).NotNull();
                        var folder =
                            Path.GetDirectoryName(reader.Entry.Key)
                            ?? throw new InvalidOperationException(
                                "Entry key must have a directory name."
                            );
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
        }
        VerifyFiles();
    }

    [Fact]
    public async ValueTask Rar_Reader_Audio_program_Async()
    {
        using (
            var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.Audio_program.rar"))
        )
        await using (
            var reader = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(stream),
                new ReaderOptions { LookForHeader = true }
            )
        )
        {
            while (await reader.MoveToNextEntryAsync())
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        CompareFilesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "test.dat"),
            Path.Combine(MISC_TEST_FILES_PATH, "test.dat")
        );
    }

    [Fact]
    public async ValueTask Rar_Jpg_Reader_Async()
    {
        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg")))
        using (
            IReader baseReader = RarReader.OpenReader(
                stream,
                new ReaderOptions { LookForHeader = true }
            )
        )
        {
            IAsyncReader reader = (IAsyncReader)baseReader;
            while (await reader.MoveToNextEntryAsync())
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async ValueTask Rar_Solid_Reader_Async() =>
        await ReadAsync("Rar.solid.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar_Comment_Reader_Async() =>
        await ReadAsync("Rar.comment.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar5_Comment_Reader_Async() =>
        await ReadAsync("Rar5.comment.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar5_Solid_Reader_Async() =>
        await ReadAsync("Rar5.solid.rar", CompressionType.Rar);

    [Fact]
    public async ValueTask Rar_Solid_Skip_Reader_Async() =>
        await DoRar_Solid_Skip_Reader_Async("Rar.solid.rar");

    [Fact]
    public async ValueTask Rar5_Solid_Skip_Reader_Async() =>
        await DoRar_Solid_Skip_Reader_Async("Rar5.solid.rar");

    private async ValueTask DoRar_Solid_Skip_Reader_Async(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            new ReaderOptions { LookForHeader = true }
        );
        while (await reader.MoveToNextEntryAsync())
        {
            if (reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
    }

    [Fact]
    public async ValueTask Rar_Reader_Skip_Async() => await DoRar_Reader_Skip_Async("Rar.rar");

    [Fact]
    public async ValueTask Rar5_Reader_Skip_Async() => await DoRar_Reader_Skip_Async("Rar5.rar");

    private async ValueTask DoRar_Reader_Skip_Async(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            new ReaderOptions { LookForHeader = true }
        );
        while (await reader.MoveToNextEntryAsync())
        {
            if (reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
    }

    private async ValueTask ReadAsync(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions? readerOptions = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using Stream stream = File.OpenRead(testArchive);
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            readerOptions ?? new ReaderOptions()
        );
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }
}
