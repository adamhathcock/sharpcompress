using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipWriterTests : WriterTests
{
    public ZipWriterTests()
        : base(ArchiveType.Zip) { }

    [Fact]
    public void Zip_BZip2_Write_EmptyFile()
    {
        // Test that writing an empty file with BZip2 compression doesn't throw DivideByZeroException
        using var memoryStream = new MemoryStream();
        var options = new WriterOptions(CompressionType.BZip2)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = new UTF8Encoding(false) },
        };

        using (var writer = WriterFactory.OpenWriter(memoryStream, ArchiveType.Zip, options))
        {
            writer.Write("test-folder/zero-byte-file.txt", Stream.Null);
        }

        Assert.True(memoryStream.Length > 0);
    }

    [Fact]
    public void Zip_BZip2_Write_EmptyFolder()
    {
        // Test that writing an empty folder entry with BZip2 compression doesn't throw DivideByZeroException
        using var memoryStream = new MemoryStream();
        var options = new WriterOptions(CompressionType.BZip2)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = new UTF8Encoding(false) },
        };

        using (var writer = WriterFactory.OpenWriter(memoryStream, ArchiveType.Zip, options))
        {
            writer.Write("test-empty-folder/", Stream.Null);
        }

        Assert.True(memoryStream.Length > 0);
    }

    [Fact]
    public void Zip_Deflate_Write() =>
        Write(
            CompressionType.Deflate,
            "Zip.deflate.noEmptyDirs.zip",
            "Zip.deflate.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_BZip2_Write() =>
        Write(
            CompressionType.BZip2,
            "Zip.bzip2.noEmptyDirs.zip",
            "Zip.bzip2.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_None_Write() =>
        Write(
            CompressionType.None,
            "Zip.none.noEmptyDirs.zip",
            "Zip.none.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_LZMA_Write() =>
        Write(
            CompressionType.LZMA,
            "Zip.lzma.noEmptyDirs.zip",
            "Zip.lzma.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_PPMd_Write() =>
        Write(
            CompressionType.PPMd,
            "Zip.ppmd.noEmptyDirs.zip",
            "Zip.ppmd.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_Rar_Write() =>
        Assert.Throws<InvalidFormatException>(() =>
            Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip")
        );

    [Fact]
    public void Zip_Deflate_Encrypted_Aes256_WriteAndRead()
    {
        const string password = "test_password";
        const string testContent = "Hello, this is a test file for encrypted ZIP.";

        using var memoryStream = new MemoryStream();

        // Write encrypted ZIP
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            Password = password,
            EncryptionType = ZipEncryptionType.Aes256,
        };

        using (var writer = new ZipWriter(memoryStream, options))
        {
            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            writer.Write("test.txt", new MemoryStream(contentBytes));
        }

        // Read back the encrypted ZIP
        memoryStream.Position = 0;

        using var archive = ZipArchive.Open(
            memoryStream,
            new ReaderOptions { Password = password }
        );

        var entry = archive.Entries.First(e => !e.IsDirectory);
        Assert.Equal("test.txt", entry.Key);

        using var entryStream = entry.OpenEntryStream();
        using var reader = new StreamReader(entryStream);
        var content = reader.ReadToEnd();

        Assert.Equal(testContent, content);
    }

    [Fact]
    public void Zip_Deflate_Encrypted_Aes128_WriteAndRead()
    {
        const string password = "test_password";
        const string testContent = "Hello, this is a test file for encrypted ZIP with AES-128.";

        using var memoryStream = new MemoryStream();

        // Write encrypted ZIP
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            Password = password,
            EncryptionType = ZipEncryptionType.Aes128,
        };

        using (var writer = new ZipWriter(memoryStream, options))
        {
            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            writer.Write("test.txt", new MemoryStream(contentBytes));
        }

        // Read back the encrypted ZIP
        memoryStream.Position = 0;

        using var archive = ZipArchive.Open(
            memoryStream,
            new ReaderOptions { Password = password }
        );

        var entry = archive.Entries.First(e => !e.IsDirectory);
        Assert.Equal("test.txt", entry.Key);

        using var entryStream = entry.OpenEntryStream();
        using var reader = new StreamReader(entryStream);
        var content = reader.ReadToEnd();

        Assert.Equal(testContent, content);
    }

    [Fact]
    public void Zip_None_Encrypted_Aes256_WriteAndRead()
    {
        const string password = "test_password";
        const string testContent = "Uncompressed but encrypted content.";

        using var memoryStream = new MemoryStream();

        // Write encrypted ZIP with no compression
        var options = new ZipWriterOptions(CompressionType.None)
        {
            Password = password,
            EncryptionType = ZipEncryptionType.Aes256,
        };

        using (var writer = new ZipWriter(memoryStream, options))
        {
            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            writer.Write("uncompressed.txt", new MemoryStream(contentBytes));
        }

        // Read back the encrypted ZIP
        memoryStream.Position = 0;

        using var archive = ZipArchive.Open(
            memoryStream,
            new ReaderOptions { Password = password }
        );

        var entry = archive.Entries.First(e => !e.IsDirectory);
        Assert.Equal("uncompressed.txt", entry.Key);

        using var entryStream = entry.OpenEntryStream();
        using var reader = new StreamReader(entryStream);
        var content = reader.ReadToEnd();

        Assert.Equal(testContent, content);
    }

    [Fact]
    public void Zip_Encrypted_MultipleFiles_WriteAndRead()
    {
        const string password = "multi_file_password";

        using var memoryStream = new MemoryStream();

        // Write encrypted ZIP with multiple files
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            Password = password,
            EncryptionType = ZipEncryptionType.Aes256,
        };

        using (var writer = new ZipWriter(memoryStream, options))
        {
            writer.Write(
                "file1.txt",
                new MemoryStream(Encoding.UTF8.GetBytes("Content of file 1"))
            );
            writer.Write(
                "file2.txt",
                new MemoryStream(Encoding.UTF8.GetBytes("Content of file 2"))
            );
            writer.Write(
                "folder/file3.txt",
                new MemoryStream(Encoding.UTF8.GetBytes("Content of file 3"))
            );
        }

        // Read back the encrypted ZIP
        memoryStream.Position = 0;

        using var archive = ZipArchive.Open(
            memoryStream,
            new ReaderOptions { Password = password }
        );

        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        Assert.Equal(3, entries.Count);

        foreach (var entry in entries)
        {
            using var entryStream = entry.OpenEntryStream();
            using var reader = new StreamReader(entryStream);
            var content = reader.ReadToEnd();
            Assert.Contains("Content of file", content);
        }
    }

    [Fact]
    public void Zip_Encrypted_DefaultEncryption_WhenPasswordSet()
    {
        const string password = "auto_encryption";
        const string testContent = "Auto encryption type test.";

        using var memoryStream = new MemoryStream();

        // Write ZIP with password but no explicit encryption type
        // Should default to AES-256
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            Password = password,
            // EncryptionType not set, should default to AES-256 when password is provided
        };

        using (var writer = new ZipWriter(memoryStream, options))
        {
            writer.Write("auto.txt", new MemoryStream(Encoding.UTF8.GetBytes(testContent)));
        }

        // Read back the encrypted ZIP
        memoryStream.Position = 0;

        using var archive = ZipArchive.Open(
            memoryStream,
            new ReaderOptions { Password = password }
        );

        var entry = archive.Entries.First(e => !e.IsDirectory);
        using var entryStream = entry.OpenEntryStream();
        using var reader = new StreamReader(entryStream);
        var content = reader.ReadToEnd();

        Assert.Equal(testContent, content);
    }
}
