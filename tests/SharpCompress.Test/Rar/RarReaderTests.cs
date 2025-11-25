using System;
using System.Collections;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarReaderTests : ReaderTests
{
    [Fact]
    public void Rar_Multi_Reader() =>
        DoRar_Multi_Reader([
            "Rar.multi.part01.rar",
            "Rar.multi.part02.rar",
            "Rar.multi.part03.rar",
            "Rar.multi.part04.rar",
            "Rar.multi.part05.rar",
            "Rar.multi.part06.rar",
        ]);

    [Fact]
    public void Rar5_Multi_Reader() =>
        DoRar_Multi_Reader([
            "Rar5.multi.part01.rar",
            "Rar5.multi.part02.rar",
            "Rar5.multi.part03.rar",
            "Rar5.multi.part04.rar",
            "Rar5.multi.part05.rar",
            "Rar5.multi.part06.rar",
        ]);

    private void DoRar_Multi_Reader(string[] archives)
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
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_Multi_Reader_Encrypted() =>
        DoRar_Multi_Reader_Encrypted([
            "Rar.EncryptedParts.part01.rar",
            "Rar.EncryptedParts.part02.rar",
            "Rar.EncryptedParts.part03.rar",
            "Rar.EncryptedParts.part04.rar",
            "Rar.EncryptedParts.part05.rar",
            "Rar.EncryptedParts.part06.rar",
        ]);

    private void DoRar_Multi_Reader_Encrypted(string[] archives) =>
        Assert.Throws<InvalidFormatException>(() =>
        {
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
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
            VerifyFiles();
        });

    [Fact]
    public void Rar_Multi_Reader_Delete_Files() =>
        DoRar_Multi_Reader_Delete_Files([
            "Rar.multi.part01.rar",
            "Rar.multi.part02.rar",
            "Rar.multi.part03.rar",
            "Rar.multi.part04.rar",
            "Rar.multi.part05.rar",
            "Rar.multi.part06.rar",
        ]);

    [Fact]
    public void Rar5_Multi_Reader_Delete_Files() =>
        DoRar_Multi_Reader_Delete_Files([
            "Rar5.multi.part01.rar",
            "Rar5.multi.part02.rar",
            "Rar5.multi.part03.rar",
            "Rar5.multi.part04.rar",
            "Rar5.multi.part05.rar",
            "Rar5.multi.part06.rar",
        ]);

    private void DoRar_Multi_Reader_Delete_Files(string[] archives)
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
                reader.WriteEntryToDirectory(
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
    public void Rar_None_Reader() => Read("Rar.none.rar", CompressionType.Rar);

    [Fact]
    public void Rar5_None_Reader() => Read("Rar5.none.rar", CompressionType.Rar);

    [Fact]
    public void Rar_Reader() => Read("Rar.rar", CompressionType.Rar);

    [Fact]
    public void Rar5_Reader() => Read("Rar5.rar", CompressionType.Rar);

    [Fact]
    public void Rar5_CRC_Blake2_Reader() => Read("Rar5.crc_blake2.rar", CompressionType.Rar);

    [Fact]
    public void Rar_EncryptedFileAndHeader_Reader() =>
        ReadRar("Rar.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public void Rar5_EncryptedFileAndHeader_Reader() =>
        ReadRar("Rar5.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public void Rar_EncryptedFileOnly_Reader() => ReadRar("Rar.encrypted_filesOnly.rar", "test");

    [Fact]
    public void Rar5_EncryptedFileOnly_Reader() => ReadRar("Rar5.encrypted_filesOnly.rar", "test");

    [Fact]
    public void Rar_Encrypted_Reader() => ReadRar("Rar.Encrypted.rar", "test");

    [Fact]
    public void Rar5_Encrypted_Reader() => ReadRar("Rar5.encrypted_filesOnly.rar", "test");

    private void ReadRar(string testArchive, string password) =>
        Read(testArchive, CompressionType.Rar, new ReaderOptions { Password = password });

    [Fact]
    public void Rar_Entry_Stream() => DoRar_Entry_Stream("Rar.rar");

    [Fact]
    public void Rar5_Entry_Stream() => DoRar_Entry_Stream("Rar5.rar");

    private void DoRar_Entry_Stream(string filename)
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
        using (var reader = ReaderFactory.Open(stream))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                    using var entryStream = reader.OpenEntryStream();
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
                    entryStream.CopyTo(fs);
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_Reader_Audio_program()
    {
        using (
            var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.Audio_program.rar"))
        )
        using (var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            while (reader.MoveToNextEntry())
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                reader.WriteEntryToDirectory(
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
    public void Rar_Jpg_Reader()
    {
        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg")))
        using (var reader = RarReader.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            while (reader.MoveToNextEntry())
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_Solid_Reader() => Read("Rar.solid.rar", CompressionType.Rar);

    [Fact]
    public void Rar_Comment_Reader() => Read("Rar.comment.rar", CompressionType.Rar);

    [Fact]
    public void Rar5_Comment_Reader() => Read("Rar5.comment.rar", CompressionType.Rar);

    [Fact]
    public void Rar5_Solid_Reader() => Read("Rar5.solid.rar", CompressionType.Rar);

    [Fact]
    public void Rar_Solid_Skip_Reader() => DoRar_Solid_Skip_Reader("Rar.solid.rar");

    [Fact]
    public void Rar5_Solid_Skip_Reader() => DoRar_Solid_Skip_Reader("Rar5.solid.rar");

    private void DoRar_Solid_Skip_Reader(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true });
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    [Fact]
    public void Rar_Reader_Skip() => DoRar_Reader_Skip("Rar.rar");

    [Fact]
    public void Rar5_Reader_Skip() => DoRar_Reader_Skip("Rar5.rar");

    private void DoRar_Reader_Skip(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true });
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.Key.NotNull().Contains("jpg"))
            {
                Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    [Fact]
    public void Rar_SkipEncryptedFilesWithoutPassword()
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.encrypted_filesOnly.rar")
        );
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true });
        while (reader.MoveToNextEntry())
        {
            //
        }
    }

    [Fact]
    public void Rar_Iterate_Reader() =>
        Iterate(
            "Rar.rar",
            "Failure jpg exe Empty jpg\\test.jpg exe\\test.exe тест.txt",
            CompressionType.Rar
        );

    [Fact]
    public void Rar2_Iterate_Archive() =>
        Iterate(
            "Rar2.rar",
            "Failure Empty тест.txt jpg\\test.jpg exe\\test.exe jpg exe",
            CompressionType.Rar
        );

    [Fact]
    public void Rar4_Iterate_Archive() =>
        Iterate(
            "Rar4.rar",
            "Failure Empty jpg exe тест.txt jpg\\test.jpg exe\\test.exe",
            CompressionType.Rar
        );

    [Fact]
    public void Rar5_Iterate_Archive() =>
        Iterate(
            "Rar5.rar",
            "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe",
            CompressionType.Rar
        );

    [Fact]
    public void Rar_Encrypted_Iterate_Archive() =>
        Iterate(
            "Rar.encrypted_filesOnly.rar",
            "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe",
            CompressionType.Rar
        );

    [Fact]
    public void Rar5_Encrypted_Iterate_Archive() =>
        Assert.Throws<CryptographicException>(() =>
            Iterate(
                "Rar5.encrypted_filesOnly.rar",
                "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe",
                CompressionType.Rar
            )
        );

    [Fact]
    public void Rar_Iterate_Multipart()
    {
        var expectedOrder = new Stack(
            new[]
            {
                "Failure",
                "jpg",
                "exe",
                "Empty",
                "тест.txt",
                Path.Combine("jpg", "test.jpg"),
                Path.Combine("exe", "test.exe"),
            }
        );
        using var reader = RarReader.Open([
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part01.rar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part02.rar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part03.rar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part04.rar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part05.rar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part06.rar"),
        ]);
        while (reader.MoveToNextEntry())
        {
            Assert.Equal(expectedOrder.Pop(), reader.Entry.Key);
        }
    }
}
