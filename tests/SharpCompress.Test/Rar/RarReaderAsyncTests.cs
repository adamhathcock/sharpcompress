using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarReaderAsyncTests : ReaderTests
{
    [Fact]
    public async Task Rar_Multi_Reader_Async() =>
        await DoRar_Multi_Reader_Async([
            "Rar.multi.part01.rar",
            "Rar.multi.part02.rar",
            "Rar.multi.part03.rar",
            "Rar.multi.part04.rar",
            "Rar.multi.part05.rar",
            "Rar.multi.part06.rar",
        ]);

    [Fact]
    public async Task Rar5_Multi_Reader_Async() =>
        await DoRar_Multi_Reader_Async([
            "Rar5.multi.part01.rar",
            "Rar5.multi.part02.rar",
            "Rar5.multi.part03.rar",
            "Rar5.multi.part04.rar",
            "Rar5.multi.part05.rar",
            "Rar5.multi.part06.rar",
        ]);

    private async Task DoRar_Multi_Reader_Async(string[] archives)
    {
        using (
            var reader = RarReader.Open(
                archives
                    .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                    .Select(p => File.OpenRead(p))
            )
        )
        {
            while (reader.MoveToNextEntry())
            {
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_Multi_Reader_Encrypted_Async() =>
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
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
                var reader = RarReader.Open(
                    archives
                        .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                        .Select(p => File.OpenRead(p)),
                    new ReaderOptions { Password = "test" }
                )
            )
            {
                while (reader.MoveToNextEntry())
                {
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
            VerifyFiles();
        });

    [Fact]
    public async Task Rar_Multi_Reader_Delete_Files_Async() =>
        await DoRar_Multi_Reader_Delete_Files_Async([
            "Rar.multi.part01.rar",
            "Rar.multi.part02.rar",
            "Rar.multi.part03.rar",
            "Rar.multi.part04.rar",
            "Rar.multi.part05.rar",
            "Rar.multi.part06.rar",
        ]);

    [Fact]
    public async Task Rar5_Multi_Reader_Delete_Files_Async() =>
        await DoRar_Multi_Reader_Delete_Files_Async([
            "Rar5.multi.part01.rar",
            "Rar5.multi.part02.rar",
            "Rar5.multi.part03.rar",
            "Rar5.multi.part04.rar",
            "Rar5.multi.part05.rar",
            "Rar5.multi.part06.rar",
        ]);

    private async Task DoRar_Multi_Reader_Delete_Files_Async(string[] archives)
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
        using (var reader = RarReader.Open(streams))
        {
            while (reader.MoveToNextEntry())
            {
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
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
    public async Task Rar_None_Reader_Async() =>
        await ReadAsync("Rar.none.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar5_None_Reader_Async() =>
        await ReadAsync("Rar5.none.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar_Reader_Async() => await ReadAsync("Rar.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar5_Reader_Async() => await ReadAsync("Rar5.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar5_CRC_Blake2_Reader_Async() =>
        await ReadAsync("Rar5.crc_blake2.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar_EncryptedFileAndHeader_Reader_Async() =>
        await ReadRar_Async("Rar.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public async Task Rar5_EncryptedFileAndHeader_Reader_Async() =>
        await ReadRar_Async("Rar5.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public async Task Rar_EncryptedFileOnly_Reader_Async() =>
        await ReadRar_Async("Rar.encrypted_filesOnly.rar", "test");

    [Fact]
    public async Task Rar5_EncryptedFileOnly_Reader_Async() =>
        await ReadRar_Async("Rar5.encrypted_filesOnly.rar", "test");

    [Fact]
    public async Task Rar_Encrypted_Reader_Async() =>
        await ReadRar_Async("Rar.Encrypted.rar", "test");

    [Fact]
    public async Task Rar5_Encrypted_Reader_Async() =>
        await ReadRar_Async("Rar5.encrypted_filesOnly.rar", "test");

    private async Task ReadRar_Async(string testArchive, string password) =>
        await ReadAsync(
            testArchive,
            CompressionType.Rar,
            new ReaderOptions { Password = password }
        );

    [Fact]
    public async Task Rar_Entry_Stream_Async() => await DoRar_Entry_Stream_Async("Rar.rar");

    [Fact]
    public async Task Rar5_Entry_Stream_Async() => await DoRar_Entry_Stream_Async("Rar5.rar");

    private async Task DoRar_Entry_Stream_Async(string filename)
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
        using (var reader = ReaderFactory.Open(stream))
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
                            ?? throw new ArgumentNullException();
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
    public async Task Rar_Reader_Audio_program_Async()
    {
        using (
            var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.Audio_program.rar"))
        )
        using (var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            while (reader.MoveToNextEntry())
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        CompareFilesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "test.dat"),
            Path.Combine(MISC_TEST_FILES_PATH, "test.dat")
        );
    }

    [Fact]
    public async Task Rar_Jpg_Reader_Async()
    {
        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg")))
        using (var reader = RarReader.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            while (reader.MoveToNextEntry())
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_Solid_Reader_Async() =>
        await ReadAsync("Rar.solid.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar_Comment_Reader_Async() =>
        await ReadAsync("Rar.comment.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar5_Comment_Reader_Async() =>
        await ReadAsync("Rar5.comment.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar5_Solid_Reader_Async() =>
        await ReadAsync("Rar5.solid.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar_Solid_Skip_Reader_Async() =>
        await DoRar_Solid_Skip_Reader_Async("Rar.solid.rar");

    [Fact]
    public async Task Rar5_Solid_Skip_Reader_Async() =>
        await DoRar_Solid_Skip_Reader_Async("Rar5.solid.rar");

    private async Task DoRar_Solid_Skip_Reader_Async(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true });
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    [Fact]
    public async Task Rar_Reader_Skip_Async() => await DoRar_Reader_Skip_Async("Rar.rar");

    [Fact]
    public async Task Rar5_Reader_Skip_Async() => await DoRar_Reader_Skip_Async("Rar5.rar");

    private async Task DoRar_Reader_Skip_Async(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true });
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    private async Task ReadAsync(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions? readerOptions = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using Stream stream = File.OpenRead(testArchive);
        using var reader = ReaderFactory.Open(stream, readerOptions ?? new ReaderOptions());
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }
}
