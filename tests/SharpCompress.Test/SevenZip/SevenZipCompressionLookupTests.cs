using System;
using System.IO;
using System.Reflection;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilities;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipCompressionLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReportsCodecWithOrWithoutAes(bool encrypted)
    {
        var part = CreatePart(encrypted, true);
        Assert.Equal(CompressionType.LZMA, part.CompressionType);
        Assert.Equal(CompressionType.LZMA, part.CompressionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingCodecRetainsInvalidFormatError(bool encrypted) =>
        Assert.Throws<InvalidFormatException>(() => CreatePart(encrypted, false).CompressionType);

#if !LEGACY_DOTNET && !DEBUG
    [Fact]
    public void UncachedLookupDoesNotAllocate()
    {
        var part = CreatePart(true, true);
        // Bind once to bypass the public property's cache without measuring reflection/boxing.
        var lookup =
            (Func<CompressionType>)
                Delegate.CreateDelegate(
                    typeof(Func<CompressionType>),
                    part,
                    typeof(SevenZipFilePart).GetMethod(
                        "GetCompression",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )!
                );
        _ = lookup();
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            _ = lookup();
        }
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - start, 0, 1024);
    }
#endif

    private static SevenZipFilePart CreatePart(bool encrypted, bool codec)
    {
        var folder = new CFolder();
        if (encrypted)
        {
            folder._coders.Add(new CCoderInfo { _methodId = CMethodId.K_AES });
        }
        if (codec)
        {
            folder._coders.Add(new CCoderInfo { _methodId = CMethodId.K_LZMA2 });
        }
        var database = new ArchiveDatabase(new PasswordProvider());
        database._folders.Add(folder);
        database._fileIndexToFolderIndexMap.Add(0);
        return new SevenZipFilePart(
            Stream.Null,
            database,
            0,
            new CFileItem(),
            new ArchiveEncoding()
        );
    }

    private sealed class PasswordProvider : IPasswordProvider
    {
        public string? CryptoGetTextPassword() => null;
    }
}
