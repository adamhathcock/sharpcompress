using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipPasswordExceptionTests : ArchiveTests
{
    // 206-byte fixture generated with 7zz a -psecret -mhe=on -m0=LZMA2
    // encrypted-header.7z data.txt (data.txt contains "hello"). No external tool needed in CI.
    private const string EncryptedHeader =
        "N3q8ryccAAQx1ES9gAAAAAAAAAAuAAAAAAAAADjCO/gWFzzAowVAn1/zBdIMi+5r2AyhxUL1SKmLFzNxH6VJqIWJTTzQDTAhcddAjSIgK/e92hRdPfRIZYJaEPZcue3MFygwZTMOKvgnqcH5B+k/o2345MpVIuA4n2yg4ag99CRyj4LF52P188ujA4nEMXCEsl5lKfQmln5QFOXywSf2ShcGEAEJcAAHCwEAASQG8QcBElMPq2bT4KLY1zYjazQMOzZJ1AxqCgGTarjeAAA=";

    [Theory]
    [InlineData(false, "secret")]
    [InlineData(true, "secret")]
    [InlineData(false, "wrong")]
    [InlineData(true, "wrong")]
    public async Task SevenZipArchive_EncryptedHeader_PasswordErrors(bool async, string password)
    {
        using var input = new MemoryStream(Convert.FromBase64String(EncryptedHeader));
        async Task ReadArchive()
        {
            if (async)
            {
                await using var archive = await SevenZipArchive.OpenAsyncArchive(
                    input,
                    new ReaderOptions { Password = password, LeaveStreamOpen = true },
                    CancellationToken.None
                );
                var count = 0;
                await foreach (var entry in archive.EntriesAsync)
                {
                    Assert.Equal("data.txt", entry.Key);
                    count++;
                }
                Assert.Equal(1, count);
            }
            else
            {
                using var archive = SevenZipArchive.OpenArchive(
                    input,
                    new ReaderOptions { Password = password, LeaveStreamOpen = true }
                );
                Assert.Equal("data.txt", archive.Entries.Single().Key);
            }
        }
        if (password == "secret")
        {
            await ReadArchive();
        }
        else
        {
            var exception = await Assert.ThrowsAsync<CryptographicException>(ReadArchive);
            Assert.IsType<InvalidFormatException>(exception.InnerException);
        }
    }

    [Fact]
    public async Task SevenZipArchive_WrongPassword_AsyncOpenPublicException()
    {
        await using var archive = await SevenZipArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA.Aes.7z"),
            new ReaderOptions { Password = "wrong" },
            CancellationToken.None
        );
        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
        {
            await foreach (var entry in archive.EntriesAsync)
            {
                if (!entry.IsDirectory)
                {
                    using var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                    await stream.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
                }
            }
        });
        Assert.IsType<DataErrorException>(exception.InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_WrongPassword_PublicException(bool async)
    {
        using var archive = SevenZipArchive.OpenArchive(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA.Aes.7z"),
            new ReaderOptions { Password = "wrong" }
        );
        var entry = archive.Entries.First(e => !e.IsDirectory);
        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
        {
            using var stream = entry.OpenEntryStream();
            if (async)
            {
                await stream.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
            }
            else
            {
                stream.CopyTo(Stream.Null);
            }
        });
        Assert.IsType<DataErrorException>(exception.InnerException);
        Assert.Contains("corrupt encrypted", exception.Message);
    }
}
