using System;
using System.IO;
using SharpCompress.Archives;
using Xunit;

namespace SharpCompress.Test;

public class ArchiveFactoryCompressedTarTests : TestBase
{
    [Fact]
    public void ArchiveFactory_Open_TarBz2_ThrowsHelpfulException()
    {
        var testFile = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var archive = ArchiveFactory.Open(testFile);
        });

        Assert.Contains("tar.bz2", exception.Message);
        Assert.Contains("ReaderFactory", exception.Message);
    }

    [Fact]
    public void ArchiveFactory_Open_TarLz_ThrowsHelpfulException()
    {
        var testFile = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz");
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var archive = ArchiveFactory.Open(testFile);
        });

        Assert.Contains("tar.lz", exception.Message);
        Assert.Contains("ReaderFactory", exception.Message);
    }

    [Fact]
    public void ArchiveFactory_Open_TarBz2Stream_ThrowsHelpfulException()
    {
        var testFile = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2");
        using var stream = File.OpenRead(testFile);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var archive = ArchiveFactory.Open(stream);
        });

        Assert.Contains("tar.bz2", exception.Message);
        Assert.Contains("ReaderFactory", exception.Message);
    }

    [Fact]
    public void ArchiveFactory_Open_TarLzStream_ThrowsHelpfulException()
    {
        var testFile = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz");
        using var stream = File.OpenRead(testFile);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var archive = ArchiveFactory.Open(stream);
        });

        Assert.Contains("tar.lz", exception.Message);
        Assert.Contains("ReaderFactory", exception.Message);
    }
}
