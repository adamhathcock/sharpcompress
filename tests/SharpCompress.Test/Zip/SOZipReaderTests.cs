using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common.Zip.SOZip;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

public class SoZipReaderTests : TestBase
{
    [Fact]
    public async Task SOZip_Reader()
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, "foo.zip");
        using Stream stream = new ForwardOnlyStream(File.OpenRead(path));
        using var reader = ZipReader.Open(stream);
        while (await reader.MoveToNextEntryAsync())
        {
            Assert.True(reader.Entry.IsSozip, $"Entry {reader.Entry.Key} is not SOZip");
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToAsync(Stream.Null);
            }
        }
    }

    [Fact]
    public void SOZip_Archive()
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, "foo.zip");
        using Stream stream = File.OpenRead(path);
        using var archive = ZipArchive.Open(stream);
        foreach (var entry in archive.Entries)
        {
            if (entry.IsSozip)
                Console.WriteLine($"{entry.Key} has SOZip random access support");
            if (entry.IsSozipIndexFile)
                Console.WriteLine($"{entry.Key} is a SOZip index file");
        }
    }
}
