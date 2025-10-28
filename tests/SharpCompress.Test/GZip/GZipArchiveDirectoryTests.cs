using System;
using System.IO;
using SharpCompress.Archives.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipArchiveDirectoryTests : TestBase
{
    [Fact]
    public void GZipArchive_AddDirectoryEntry_ThrowsNotSupportedException()
    {
        using var archive = GZipArchive.Create();

        Assert.Throws<NotSupportedException>(() =>
            archive.AddDirectoryEntry("test-dir", DateTime.Now)
        );
    }
}
