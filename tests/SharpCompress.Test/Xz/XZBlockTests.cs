using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzBlockTests : XzTestsBase
{
    protected override void Rewind(Stream stream) => stream.Position = 12;

    protected override void RewindIndexed(Stream stream) => stream.Position = 12;

    private byte[] ReadBytes(XZBlock block, int bytesToRead)
    {
        var buffer = new byte[bytesToRead];
        var read = block.Read(buffer, 0, bytesToRead);
        if (read != bytesToRead)
        {
            throw new EndOfStreamException();
        }

        return buffer;
    }

    [Fact]
    public void OnFindIndexBlockThrow()
    {
        var bytes = new byte[] { 0 };
        using Stream indexBlockStream = new MemoryStream(bytes);
        var xzBlock = new XZBlock(indexBlockStream, CheckType.CRC64, 8);
        Assert.Throws<XZIndexMarkerReachedException>(() =>
        {
            ReadBytes(xzBlock, 1);
        });
    }

    [Fact]
    public void CrcIncorrectThrows()
    {
        var bytes = (byte[])Compressed.Clone();
        bytes[20]++;
        using Stream badCrcStream = new MemoryStream(bytes);
        Rewind(badCrcStream);
        var xzBlock = new XZBlock(badCrcStream, CheckType.CRC64, 8);
        var ex = Assert.Throws<InvalidFormatException>(() =>
        {
            ReadBytes(xzBlock, 1);
        });
        Assert.Equal("Block header corrupt", ex.Message);
    }

    [Fact]
    public void CanReadM()
    {
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        Assert.Equal(Encoding.ASCII.GetBytes("M"), ReadBytes(xzBlock, 1));
    }

    [Fact]
    public void CanReadMary()
    {
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        Assert.Equal(Encoding.ASCII.GetBytes("M"), ReadBytes(xzBlock, 1));
        Assert.Equal(Encoding.ASCII.GetBytes("a"), ReadBytes(xzBlock, 1));
        Assert.Equal(Encoding.ASCII.GetBytes("ry"), ReadBytes(xzBlock, 2));
    }

    [Fact]
    public void CanReadPoemWithStreamReader()
    {
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        Assert.Equal(sr.ReadToEnd(), Original);
    }

    [Fact]
    public void NoopWhenNoPadding()
    {
        // CompressedStream's only block has no padding.
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        sr.ReadToEnd();
        Assert.Equal(0L, CompressedStream.Position % 4L);
    }

    [Fact]
    public void SkipsPaddingWhenPresent()
    {
        // CompressedIndexedStream's first block has 1-byte padding.
        var xzBlock = new XZBlock(CompressedIndexedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        sr.ReadToEnd();
        Assert.Equal(0L, CompressedIndexedStream.Position % 4L);
    }

    [Fact]
    public void HandlesPaddingInUnalignedBlock()
    {
        var compressedUnaligned = new byte[Compressed.Length + 1];
        Compressed.CopyTo(compressedUnaligned, 1);
        var compressedUnalignedStream = new MemoryStream(compressedUnaligned);
        compressedUnalignedStream.Position = 13;

        // Compressed's only block has no padding.
        var xzBlock = new XZBlock(compressedUnalignedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        sr.ReadToEnd();
        Assert.Equal(1L, compressedUnalignedStream.Position % 4L);
    }
}
