using System.IO;
using SharpCompress.Common.SevenZip;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipWriterAttributesTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void SevenZipFilesInfo_MixedEntries_AttributesAreAllDefined(int count)
    {
        var entries = new SevenZipWriteEntry[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] =
                i == 0
                    ? new SevenZipWriteEntry
                    {
                        Name = "dir",
                        IsDirectory = true,
                        IsEmpty = true,
                        Attributes = 0x10,
                    }
                    : new SevenZipWriteEntry { Name = $"file{i}", IsEmpty = true };
        }
        using var stream = new MemoryStream();
        new SevenZipFilesInfoWriter { Entries = entries }.Write(stream);
        var bytes = stream.ToArray();
        // Attributes are the final property: id, size, all-defined, external, values, end.
        var start = bytes.Length - (count * 4 + 5);
        Assert.Equal(0x15, bytes[start]);
        Assert.Equal(count * 4 + 2, bytes[start + 1]);
        Assert.Equal(1, bytes[start + 2]);
        Assert.Equal(0, bytes[start + 3]);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(
                i == 0 ? 0x10u : 0x20u,
                System.BitConverter.ToUInt32(bytes, start + 4 + i * 4)
            );
        }
    }
}
