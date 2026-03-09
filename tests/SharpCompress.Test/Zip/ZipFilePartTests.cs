using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.IO;
using SharpCompress.Providers;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipFilePartTests
{
    [Fact]
    public void GetCryptoStream_Bounds_Known_Size_Zip64_Entries()
    {
        var header = new DirectoryEntryHeader(new ArchiveEncoding())
        {
            Name = "entry.bin",
            CompressionMethod = ZipCompressionMethod.None,
            CompressedSize = uint.MaxValue,
            UncompressedSize = uint.MaxValue,
        };

        using var backingStream = new MemoryStream([1, 2, 3, 4, 5], writable: false);
        var part = new TestZipFilePart(header, backingStream);

        using var cryptoStream = part.OpenCryptoStream();

        Assert.IsType<ReadOnlySubStream>(cryptoStream);
    }

    [Fact]
    public void GetCryptoStream_Leaves_DataDescriptor_Entries_Unbounded_When_Size_Is_Unknown()
    {
        var header = new DirectoryEntryHeader(new ArchiveEncoding())
        {
            Name = "entry.bin",
            CompressionMethod = ZipCompressionMethod.None,
            CompressedSize = 0,
            UncompressedSize = 0,
            Flags = HeaderFlags.UsePostDataDescriptor,
        };

        using var backingStream = new MemoryStream([1, 2, 3, 4, 5], writable: false);
        var part = new TestZipFilePart(header, backingStream);

        using var cryptoStream = part.OpenCryptoStream();

        Assert.IsNotType<ReadOnlySubStream>(cryptoStream);
    }

    private sealed class TestZipFilePart(ZipFileEntry header, Stream stream)
        : ZipFilePart(header, stream, CompressionProviderRegistry.Default)
    {
        public Stream OpenCryptoStream() => GetCryptoStream(CreateBaseStream());

        protected override Stream CreateBaseStream() => BaseStream;
    }
}
