using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzIndexTests : XzTestsBase
{
    protected override void RewindEmpty(Stream stream) => stream.Position = 12;

    protected override void Rewind(Stream stream) => stream.Position = 356;

    protected override void RewindIndexed(Stream stream) => stream.Position = 612;

    [Fact]
    public void RecordsStreamStartOnInit()
    {
        using Stream badStream = new MemoryStream([1, 2, 3, 4, 5]);
        var br = new BinaryReader(badStream);
        var index = new XZIndex(br, false);
        Assert.Equal(0, index.StreamStartPosition);
    }

    [Fact]
    public void ThrowsIfHasNoIndexMarker()
    {
        using Stream badStream = new MemoryStream([1, 2, 3, 4, 5]);
        var br = new BinaryReader(badStream);
        var index = new XZIndex(br, false);
        Assert.Throws<InvalidFormatException>(() => index.Process());
    }

    [Fact]
    public void ReadsNoRecord()
    {
        var br = new BinaryReader(CompressedEmptyStream);
        var index = new XZIndex(br, false);
        index.Process();
        Assert.Equal((ulong)0, index.NumberOfRecords);
    }

    [Fact]
    public void ReadsOneRecord()
    {
        var br = new BinaryReader(CompressedStream);
        var index = new XZIndex(br, false);
        index.Process();
        Assert.Equal((ulong)1, index.NumberOfRecords);
    }

    [Fact]
    public void ReadsMultipleRecords()
    {
        var br = new BinaryReader(CompressedIndexedStream);
        var index = new XZIndex(br, false);
        index.Process();
        Assert.Equal((ulong)2, index.NumberOfRecords);
    }

    [Fact]
    public void ReadsFirstRecord()
    {
        var br = new BinaryReader(CompressedStream);
        var index = new XZIndex(br, false);
        index.Process();
        Assert.Equal((ulong)OriginalBytes.Length, index.Records[0].UncompressedSize);
    }

    [Fact]
    public void SkipsPadding()
    {
        // Index with 3-byte padding.
        using Stream badStream = new MemoryStream([
            0x00,
            0x01,
            0x10,
            0x80,
            0x01,
            0x00,
            0x00,
            0x00,
            0xB1,
            0x01,
            0xD9,
            0xC9,
            0xFF,
        ]);
        var br = new BinaryReader(badStream);
        var index = new XZIndex(br, false);
        index.Process();
        Assert.Equal(0L, badStream.Position % 4L);
    }
}
