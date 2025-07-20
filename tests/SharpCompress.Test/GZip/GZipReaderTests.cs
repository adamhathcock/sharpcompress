using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipReaderTests : ReaderTests
{
    public GZipReaderTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void GZip_Reader_Generic() => Read("Tar.tar.gz", CompressionType.GZip);

    [Fact]
    public void GZip_Reader_Generic2()
    {
        //read only as GZip itme
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var reader = GZipReader.Open(new SharpCompressStream(stream));
        while (reader.MoveToNextEntry()) // Crash here
        {
            Assert.NotEqual(0, reader.Entry.Size);
            Assert.NotEqual(0, reader.Entry.Crc);
        }
    }
}
