using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipReaderTests : ReaderTests
{
    public ZipReaderTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void Issue_269_Double_Skip()
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, "PrePostHeaders.zip");
        using Stream stream = new ForwardOnlyStream(File.OpenRead(path));
        using var reader = ReaderFactory.Open(stream);
        var count = 0;
        while (reader.MoveToNextEntry())
        {
            count++;
            if (!reader.Entry.IsDirectory)
            {
                if (count % 2 != 0)
                {
                    reader.WriteEntryTo(Stream.Null);
                }
            }
        }
    }

    [Fact]
    public void Zip_Zip64_Streamed_Read() => Read("Zip.zip64.zip", CompressionType.Deflate);

    [Fact]
    public void Zip_ZipX_Streamed_Read() => Read("Zip.zipx", CompressionType.LZMA);

    [Fact]
    public void Zip_BZip2_Streamed_Read() => Read("Zip.bzip2.dd.zip", CompressionType.BZip2);

    [Fact]
    public void Zip_BZip2_Read() => Read("Zip.bzip2.zip", CompressionType.BZip2);

    [Fact]
    public void Zip_Deflate_Streamed2_Read() =>
        Read("Zip.deflate.dd-.zip", CompressionType.Deflate);

    [Fact]
    public void Zip_Deflate_Streamed_Read() => Read("Zip.deflate.dd.zip", CompressionType.Deflate);

    [Fact]
    public void Zip_Deflate_Streamed_Skip()
    {
        using Stream stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))
        );
        using var reader = ReaderFactory.Open(stream);
        var x = 0;
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                x++;
                if (x % 2 == 0)
                {
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
    }

    [Fact]
    public void Zip_Deflate_Read() => Read("Zip.deflate.zip", CompressionType.Deflate);

    [Fact]
    public void Zip_Deflate64_Read() => Read("Zip.deflate64.zip", CompressionType.Deflate64);

    [Fact]
    public void Zip_LZMA_Streamed_Read() => Read("Zip.lzma.dd.zip", CompressionType.LZMA);

    [Fact]
    public void Zip_LZMA_Read() => Read("Zip.lzma.zip", CompressionType.LZMA);

    [Fact]
    public void Zip_PPMd_Streamed_Read() => Read("Zip.ppmd.dd.zip", CompressionType.PPMd);

    [Fact]
    public void Zip_PPMd_Read() => Read("Zip.ppmd.zip", CompressionType.PPMd);

    [Fact]
    public void Zip_None_Read() => Read("Zip.none.zip", CompressionType.None);

    [Fact]
    public void Zip_Deflate_NoEmptyDirs_Read() =>
        Read("Zip.deflate.noEmptyDirs.zip", CompressionType.Deflate);

    [Fact]
    public void Zip_BZip2_PkwareEncryption_Read()
    {
        using (
            Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip"))
        )
        using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Zip_Reader_Disposal_Test()
    {
        using var stream = new TestStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))
        );
        using (var reader = ReaderFactory.Open(stream))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public void Zip_Reader_Disposal_Test2()
    {
        using var stream = new TestStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))
        );
        var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        Assert.False(stream.IsDisposed);
    }

    [Fact]
    public void Zip_LZMA_WinzipAES_Read() =>
        Assert.Throws<NotSupportedException>(() =>
        {
            using (
                Stream stream = File.OpenRead(
                    Path.Combine(TEST_ARCHIVES_PATH, "Zip.lzma.WinzipAES.zip")
                )
            )
            using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                        reader.WriteEntryToDirectory(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                }
            }
            VerifyFiles();
        });

    [Fact]
    public void Zip_Deflate_WinzipAES_Read()
    {
        using (
            Stream stream = File.OpenRead(
                Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip")
            )
        )
        using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Zip_Deflate_ZipCrypto_Read()
    {
        var count = 0;
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "zipcrypto.zip")))
        using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                    count++;
                }
            }
        }
        Assert.Equal(8, count);
    }

    [Fact]
    public void TestSharpCompressWithEmptyStream()
    {
        var expected = new[]
        {
            new Tuple<string, byte[]>("foo.txt", Array.Empty<byte>()),
            new Tuple<string, byte[]>("foo2.txt", new byte[10]),
        };

        using var memory = new MemoryStream();
        Stream stream = new TestStream(memory, read: true, write: true, seek: false);

        using (var zipWriter = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
        {
            zipWriter.Write(expected[0].Item1, new MemoryStream(expected[0].Item2));
            zipWriter.Write(expected[1].Item1, new MemoryStream(expected[1].Item2));
        }

        stream = new MemoryStream(memory.ToArray());
        File.WriteAllBytes(Path.Combine(SCRATCH_FILES_PATH, "foo.zip"), memory.ToArray());

        using IReader zipReader = ZipReader.Open(
            SharpCompressStream.Create(stream, leaveOpen: true, throwOnDispose: true)
        );
        var i = 0;
        while (zipReader.MoveToNextEntry())
        {
            using (var entry = zipReader.OpenEntryStream())
            {
                var tempStream = new MemoryStream();
                const int bufSize = 0x1000;
                var buf = new byte[bufSize];
                var bytesRead = 0;
                while ((bytesRead = entry.Read(buf, 0, bufSize)) > 0)
                {
                    tempStream.Write(buf, 0, bytesRead);
                }

                Assert.Equal(expected[i].Item1, zipReader.Entry.Key);
                Assert.Equal(expected[i].Item2, tempStream.ToArray());
            }
            i++;
        }
    }

    [Fact]
    public void Zip_None_Issue86_Streamed_Read()
    {
        var keys = new[] { "Empty1", "Empty2", "Dir1/", "Dir2/", "Fake1", "Fake2", "Internal.zip" };

        using Stream stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.issue86.zip")
        );
        using var reader = ZipReader.Open(stream);
        foreach (var key in keys)
        {
            reader.MoveToNextEntry();

            Assert.Equal(reader.Entry.Key, key);

            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
            }
        }

        Assert.False(reader.MoveToNextEntry());
    }

    [Fact]
    public void Zip_ReaderMoveToNextEntry()
    {
        var keys = new[] { "version", "sizehint", "data/0/metadata", "data/0/records" };

        using var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "test_477.zip"));
        using var reader = ZipReader.Open(fileStream);
        foreach (var key in keys)
        {
            reader.MoveToNextEntry();

            Assert.Equal(reader.Entry.Key, key);
        }
    }

    [Fact]
    public void Issue_685()
    {
        var count = 0;
        using var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Issue_685.zip"));
        using var reader = ZipReader.Open(fileStream);
        while (reader.MoveToNextEntry())
        {
            count++;
            reader.OpenEntryStream().Dispose(); // Uncomment for workaround
        }
        Assert.Equal(4, count);
    }

    [Fact]
    public void Zip_ReaderFactory_Uncompressed_Read_All()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.uncompressed.zip");
        using var stream = File.OpenRead(zipPath);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            var target = new MemoryStream();
            reader.OpenEntryStream().CopyTo(target);
        }
    }

    [Fact]
    public void Zip_ReaderFactory_Uncompressed_Skip_All()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.uncompressed.zip");
        using var stream = File.OpenRead(zipPath);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry()) { }
    }

    //this test uses a large 7zip file containing a zip file inside it to test zip64 support
    // we probably shouldn't be allowing ExtractAllEntries here but it works for now.
    [Fact]
    public void Zip_Uncompressed_64bit()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "64bitstream.zip.7z");
        using var stream = File.OpenRead(zipPath);
        var archive = ArchiveFactory.Open(stream);
        var reader = archive.ExtractAllEntries();
        reader.MoveToNextEntry();
        var zipReader = ZipReader.Open(reader.OpenEntryStream());
        var x = 0;
        while (zipReader.MoveToNextEntry())
        {
            x++;
        }

        Assert.Equal(4, x);
    }

    [Fact]
    public void Zip_Uncompressed_Encrypted_Read()
    {
        using var reader = ReaderFactory.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.encrypted.zip"),
            new ReaderOptions { Password = "test" }
        );
        reader.MoveToNextEntry();
        Assert.Equal("first.txt", reader.Entry.Key);
        Assert.Equal(199, reader.Entry.Size);
        reader.OpenEntryStream().Dispose();
        reader.MoveToNextEntry();
        Assert.Equal("second.txt", reader.Entry.Key);
        Assert.Equal(197, reader.Entry.Size);
    }

    [Fact]
    public void ZipReader_Returns_Same_Entries_As_ZipArchive()
    {
        // Verifies that ZipReader and ZipArchive return the same entries
        // for standard single-volume ZIP files. ZipReader processes LocalEntry
        // headers sequentially, while ZipArchive uses DirectoryEntry headers
        // from the central directory and seeks to LocalEntry headers for data.
        var testFiles = new[] { "Zip.none.zip", "Zip.deflate.zip", "Zip.none.issue86.zip" };

        foreach (var testFile in testFiles)
        {
            var path = Path.Combine(TEST_ARCHIVES_PATH, testFile);

            var readerKeys = new List<string>();
            using (var stream = File.OpenRead(path))
            using (var reader = ZipReader.Open(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    readerKeys.Add(reader.Entry.Key!);
                }
            }

            var archiveKeys = new List<string>();
            using (var archive = Archives.Zip.ZipArchive.Open(path))
            {
                foreach (var entry in archive.Entries)
                {
                    archiveKeys.Add(entry.Key!);
                }
            }

            Assert.Equal(archiveKeys.Count, readerKeys.Count);
            Assert.Equal(archiveKeys.OrderBy(k => k), readerKeys.OrderBy(k => k));
        }
    }
}
