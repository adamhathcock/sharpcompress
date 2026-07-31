using System.IO;
using System.IO.Compression;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
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
        using var reader = GZipReader.OpenReader(SharpCompressStream.CreateNonDisposing(stream));
        while (reader.MoveToNextEntry()) // Crash here
        {
            Assert.NotEqual(0, reader.Entry.Size);
            Assert.NotEqual(0, reader.Entry.Crc);
        }
    }

    [Fact]
    public void GZip_ReaderFactory_FlatGZip()
    {
        var source = new byte[2048];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = 0xFF;
        }

        var gzipPath = Path.Combine(SCRATCH_FILES_PATH, "Flat.bin.gz");
        using (var output = File.Create(gzipPath))
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(source, 0, source.Length);
        }

        using var stream = File.OpenRead(gzipPath);
        using var reader = ReaderFactory.OpenReader(stream);
        Assert.IsType<GZipReader>(reader);
        Assert.True(reader.MoveToNextEntry());

        using var ms = new MemoryStream();
        reader.WriteEntryTo(ms);
        Assert.Equal(source.Length, ms.Length);
        Assert.Equal(source, ms.ToArray());

        Assert.False(reader.MoveToNextEntry());
    }
}
