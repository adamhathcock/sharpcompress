using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Zip;
/*
public class ZipReaderTests : ReaderTests
{
    public ZipReaderTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task Issue_269_Double_Skip()
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, "PrePostHeaders.zip");
        using Stream stream = new ForwardOnlyStream(File.OpenRead(path));
        using var reader = ReaderFactory.Open(stream);
        var count = 0;
        while (await reader.MoveToNextEntryAsync())
        {
            count++;
            if (!reader.Entry.IsDirectory)
            {
                if (count % 2 != 0)
                {
                    await reader.WriteEntryToAsync(Stream.Null);
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
    public async Task Zip_Deflate_Streamed_Skip()
    {
        using Stream stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))
        );
        using var reader = ReaderFactory.Open(stream);
        var x = 0;
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                x++;
                if (x % 2 == 0)
                {
                    await reader.WriteEntryToDirectoryAsync(
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
    public async Task Zip_BZip2_PkwareEncryption_Read()
    {
        using (
            Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip"))
        )
        using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
        {
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Zip_Reader_Disposal_Test()
    {
        using var stream = new TestStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))
        );
        using (var reader = ReaderFactory.Open(stream))
        {
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task Zip_Reader_Disposal_Test2()
    {
        using var stream = new TestStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))
        );
        var reader = ReaderFactory.Open(stream);
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        Assert.False(stream.IsDisposed);
    }

    [Fact]
    public Task Zip_LZMA_WinzipAES_Read() =>
        Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            using (
                Stream stream = File.OpenRead(
                    Path.Combine(TEST_ARCHIVES_PATH, "Zip.lzma.WinzipAES.zip")
                )
            )
            using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                        await reader.WriteEntryToDirectoryAsync(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                }
            }
            VerifyFiles();
        });

    [Fact]
    public async Task Zip_Deflate_WinzipAES_Read()
    {
        using (
            Stream stream = File.OpenRead(
                Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip")
            )
        )
        using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
        {
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Zip_Deflate_ZipCrypto_Read()
    {
        var count = 0;
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "zipcrypto.zip")))
        using (var reader = ZipReader.Open(stream, new ReaderOptions { Password = "test" }))
        {
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(
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
    public async Task TestSharpCompressWithEmptyStream()
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
        while (await zipReader.MoveToNextEntryAsync())
        {
            using (var entry = await zipReader.OpenEntryStreamAsync())
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
    public async Task Zip_None_Issue86_Streamed_Read()
    {
        var keys = new[] { "Empty1", "Empty2", "Dir1/", "Dir2/", "Fake1", "Fake2", "Internal.zip" };

        using Stream stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.issue86.zip")
        );
        using var reader = ZipReader.Open(stream);
        foreach (var key in keys)
        {
            await reader.MoveToNextEntryAsync();

            Assert.Equal(reader.Entry.Key, key);

            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
            }
        }

        Assert.False(await reader.MoveToNextEntryAsync());
    }

    [Fact]
    public async Task Zip_ReaderMoveToNextEntryAsync()
    {
        var keys = new[] { "version", "sizehint", "data/0/metadata", "data/0/records" };

        using var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "test_477.zip"));
        using var reader = ZipReader.Open(fileStream);
        foreach (var key in keys)
        {
            await reader.MoveToNextEntryAsync();

            Assert.Equal(reader.Entry.Key, key);
        }
    }

    [Fact]
    public async Task Issue_685()
    {
        var count = 0;
        using var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Issue_685.zip"));
        using var reader = ZipReader.Open(fileStream);
        while (await reader.MoveToNextEntryAsync())
        {
            count++;
            reader.OpenEntryStreamAsync().Dispose(); // Uncomment for workaround
        }
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task Zip_ReaderFactory_Uncompressed_Read_All()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.uncompressed.zip");
        using var stream = File.OpenRead(zipPath);
        using var reader = ReaderFactory.Open(stream);
        while (await reader.MoveToNextEntryAsync())
        {
            var target = new MemoryStream();
            await (await reader.OpenEntryStreamAsync()).CopyToAsync(target);
        }
    }

    [Fact]
    public async Task Zip_ReaderFactory_Uncompressed_Skip_All()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.uncompressed.zip");
        using var stream = File.OpenRead(zipPath);
        using var reader = ReaderFactory.Open(stream);
        while (await reader.MoveToNextEntryAsync()) { }
    }

    //this test uses a large 7zip file containing a zip file inside it to test zip64 support
    // we probably shouldn't be allowing ExtractAllEntries here but it works for now.
    [Fact]
    public async Task Zip_Uncompressed_64bit()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "64bitstream.zip.7z");
        using var stream = File.OpenRead(zipPath);
        var archive = ArchiveFactory.Open(stream);
        var reader = archive.ExtractAllEntries();
        await reader.MoveToNextEntryAsync();
        var zipReader = ZipReader.Open(await reader.OpenEntryStreamAsync());
        var x = 0;
        while (await zipReader.MoveToNextEntryAsync())
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
        reader.MoveToNextEntryAsync();
        Assert.Equal("first.txt", reader.Entry.Key);
        Assert.Equal(199, reader.Entry.Size);
        reader.OpenEntryStreamAsync().Dispose();
        reader.MoveToNextEntryAsync();
        Assert.Equal("second.txt", reader.Entry.Key);
        Assert.Equal(197, reader.Entry.Size);
    }
}
*/
