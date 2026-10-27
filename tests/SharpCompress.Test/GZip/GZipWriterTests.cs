using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipWriterTests : WriterTests
{
    public GZipWriterTests()
        : base(ArchiveType.GZip) => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void GZip_Writer_Generic()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        using (
            var writer = WriterFactory.OpenWriter(
                stream,
                ArchiveType.GZip,
                new WriterOptions(CompressionType.GZip)
            )
        )
        {
            writer.Write("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }

    [Fact]
    public void GZip_Writer_Empty_Entry_Is_A_Valid_Stream()
    {
        using var output = new MemoryStream();
        using (var writer = new GZipWriter(output))
        {
            writer.Write("empty.txt", new MemoryStream());
        }

        var gz = output.ToArray();
        Assert.Equal(new byte[] { 0x1f, 0x8b }, gz.Take(2));
        using var decompressed = new System.IO.Compression.GZipStream(
            new MemoryStream(gz),
            System.IO.Compression.CompressionMode.Decompress
        );
        Assert.Equal(-1, decompressed.ReadByte());
    }

    [Fact]
    public void GZip_Writer()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        using (var writer = new GZipWriter(stream))
        {
            writer.Write("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }

    [Fact]
    public void GZip_Writer_Generic_Bad_Compression() =>
        Assert.Throws<InvalidFormatException>(() =>
        {
            using Stream stream = File.OpenWrite(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"));
            using var writer = WriterFactory.OpenWriter(
                stream,
                ArchiveType.GZip,
                new WriterOptions(CompressionType.BZip2)
            );
        });

    [Fact]
    public void GZip_Writer_Entry_Path_With_Dir()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        using (var writer = new GZipWriter(stream))
        {
            var path = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
            writer.Write(path, path); //covers issue #532
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }
}
