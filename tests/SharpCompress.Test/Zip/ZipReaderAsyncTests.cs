using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipReaderAsyncTests : ReaderTests
{
    public ZipReaderAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task Issue_269_Double_Skip_Async()
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
    public async Task Zip_Zip64_Streamed_Read_Async() =>
        await ReadAsync("Zip.zip64.zip", CompressionType.Deflate);

    [Fact]
    public async Task Zip_ZipX_Streamed_Read_Async() =>
        await ReadAsync("Zip.zipx", CompressionType.LZMA);

    [Fact]
    public async Task Zip_BZip2_Streamed_Read_Async() =>
        await ReadAsync("Zip.bzip2.dd.zip", CompressionType.BZip2);

    [Fact]
    public async Task Zip_BZip2_Read_Async() =>
        await ReadAsync("Zip.bzip2.zip", CompressionType.BZip2);

    [Fact]
    public async Task Zip_Deflate_Streamed2_Read_Async() =>
        await ReadAsync("Zip.deflate.dd-.zip", CompressionType.Deflate);

    [Fact]
    public async Task Zip_Deflate_Streamed_Read_Async() =>
        await ReadAsync("Zip.deflate.dd.zip", CompressionType.Deflate);

    [Fact]
    public async Task Zip_Deflate_Streamed_Skip_Async()
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
    public async Task Zip_Deflate_Read_Async() =>
        await ReadAsync("Zip.deflate.zip", CompressionType.Deflate);

    [Fact]
    public async Task Zip_Deflate64_Read_Async() =>
        await ReadAsync("Zip.deflate64.zip", CompressionType.Deflate64);

    [Fact]
    public async Task Zip_LZMA_Streamed_Read_Async() =>
        await ReadAsync("Zip.lzma.dd.zip", CompressionType.LZMA);

    [Fact]
    public async Task Zip_LZMA_Read_Async() =>
        await ReadAsync("Zip.lzma.zip", CompressionType.LZMA);

    [Fact]
    public async Task Zip_PPMd_Streamed_Read_Async() =>
        await ReadAsync("Zip.ppmd.dd.zip", CompressionType.PPMd);

    [Fact]
    public async Task Zip_PPMd_Read_Async() =>
        await ReadAsync("Zip.ppmd.zip", CompressionType.PPMd);

    [Fact]
    public async Task Zip_None_Read_Async() =>
        await ReadAsync("Zip.none.zip", CompressionType.None);

    [Fact]
    public async Task Zip_Deflate_NoEmptyDirs_Read_Async() =>
        await ReadAsync("Zip.deflate.noEmptyDirs.zip", CompressionType.Deflate);

    [Fact]
    public async Task Zip_BZip2_PkwareEncryption_Read_Async()
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
    public async Task Zip_Reader_Disposal_Test_Async()
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
    public async Task Zip_Reader_Disposal_Test2_Async()
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
    public async Task Zip_LZMA_WinzipAES_Read_Async() =>
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
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
    public async Task Zip_Deflate_WinzipAES_Read_Async()
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
    public async Task Zip_Deflate_ZipCrypto_Read_Async()
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
}
