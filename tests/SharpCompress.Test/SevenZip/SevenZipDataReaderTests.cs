using System;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Crypto;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipDataReaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DataReader_Reads_And_Clones_Across_Storage_Boundaries(bool pooled)
    {
        using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, Encoding.UTF8, true))
        {
            writer.Write(0x12345678u);
            writer.Write(0x123456789abcdef0ul);
        }
        data.WriteEncodedUInt64(ulong.MaxValue);
        // The surrogate pair crosses a UTF-16 decoding chunk as well as pooled blocks.
        var name = new string('x', 127) + "\U0001f600" + new string('y', 130);
        var nameBytes = Encoding.Unicode.GetBytes(name + "\0");
        data.Write(nameBytes, 0, nameBytes.Length);
        using var storage = new PooledMemoryStream(0, 7);
        var bytes = data.ToArray();
        storage.Write(bytes, 0, bytes.Length);
        var reader = pooled ? new DataReader(storage) : new DataReader(bytes, 0, bytes.Length);
        Assert.Equal(0x12345678u, reader.ReadUInt32());
        var clone = reader.Clone();
        Assert.Equal(0x123456789abcdef0ul, clone.ReadUInt64());
        Assert.Equal(4, reader.Offset);
        Assert.Equal(0x123456789abcdef0ul, reader.ReadUInt64());
        Assert.Equal(ulong.MaxValue, reader.ReadNumber());
        Assert.Equal(ulong.MaxValue, clone.ReadNumber());
        Assert.Equal(name, reader.ReadString());
        Assert.Equal(name, clone.ReadString());
        Assert.Equal(0, reader.Remaining);
        Assert.Throws<IncompleteArchiveException>(() => reader.ReadByte());
        Assert.Throws<IncompleteArchiveException>(() => reader.SkipData(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DataReader_Bounds_Bulk_Reads_And_Unterminated_Strings(bool pooled)
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        using var storage = new PooledMemoryStream(0, 3);
        storage.Write(bytes, 0, bytes.Length);
        var reader = pooled ? new DataReader(storage) : new DataReader(bytes, 0, bytes.Length);
        var output = new byte[4];
        reader.SkipData(1);
        reader.ReadBytes(output, 0, output.Length);
        Assert.Equal(new byte[] { 2, 3, 4, 5 }, output);
        Assert.Throws<IncompleteArchiveException>(() => reader.ReadBytes(output, 0, 1));
        var stringReader = pooled
            ? new DataReader(storage)
            : new DataReader(bytes, 0, bytes.Length);
        Assert.Throws<IncompleteArchiveException>(() => stringReader.ReadString());
    }
}
