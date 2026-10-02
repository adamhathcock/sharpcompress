using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar;

/// <summary>
/// Summary description for RarFactoryReaderTest
/// </summary>
public class RarHeaderFactoryTest : TestBase
{
    private readonly RarHeaderFactory _rarHeaderFactory;

    public RarHeaderFactoryTest() =>
        _rarHeaderFactory = new RarHeaderFactory(
            StreamingMode.Seekable,
            ReaderOptions.ForExternalStream with
            {
                LeaveStreamOpen = true,
            }
        );

    [Fact]
    public void Rar_ReadHeaders_RecognizeEncryptedFlag() =>
        ReadEncryptedFlag("Rar.encrypted_filesAndHeader.rar", true);

    [Fact]
    public void Rar5_ReadHeaders_RecognizeEncryptedFlag() =>
        ReadEncryptedFlag("Rar5.encrypted_filesAndHeader.rar", true);

    [Fact]
    public void Rar_ReadHeaders_RecognizeNoEncryptedFlag() => ReadEncryptedFlag("Rar.rar", false);

    [Fact]
    public void Rar5_ReadHeaders_RecognizeNoEncryptedFlag() => ReadEncryptedFlag("Rar5.rar", false);

    private void ReadEncryptedFlag(string testArchive, bool isEncrypted)
    {
        using var stream = new FileStream(
            Path.Combine(TEST_ARCHIVES_PATH, testArchive),
            FileMode.Open,
            FileAccess.Read
        );
        foreach (var header in _rarHeaderFactory.ReadHeaders(stream))
        {
            if (header.HeaderType == HeaderType.Archive || header.HeaderType == HeaderType.Crypt)
            {
                Assert.Equal(isEncrypted, _rarHeaderFactory.IsEncrypted);
                break;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar5_ReadHeaders_WithCachedKey(bool useAsync)
    {
        var options = ReaderOptions.ForExternalStream.WithPassword("test");
        Assert.Equal(6, await CountEncryptedFileHeaders(useAsync, options, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar5_ReadHeaders_RejectChangedPassword(bool useAsync)
    {
        var options = ReaderOptions.ForExternalStream.WithPassword("test");
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await CountEncryptedFileHeaders(useAsync, options, true)
        );
    }

    private async Task<int> CountEncryptedFileHeaders(
        bool useAsync,
        ReaderOptions options,
        bool changePassword
    )
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.encrypted_filesAndHeader.rar")
        );
        var factory = new RarHeaderFactory(StreamingMode.Seekable, options);
        var count = 0;
        if (useAsync)
        {
            await foreach (var header in factory.ReadHeadersAsync(stream))
            {
                CountHeader(header);
            }
        }
        else
        {
            foreach (var header in factory.ReadHeaders(stream))
            {
                CountHeader(header);
            }
        }
        return count;

        void CountHeader(IRarHeader header)
        {
            if (header.HeaderType == HeaderType.File)
            {
                count++;
                if (changePassword)
                {
                    options.Password = "failed";
                }
            }
        }
    }
}
