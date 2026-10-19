using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Zip;

public class Zip64AllocationTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(43UL)]
    [InlineData(ulong.MaxValue)]
    public async Task Zip64_Rejects_Invalid_Record_Size(ulong size)
    {
        using var stream = CreateRecord(size, 0);
        Assert.Throws<InvalidFormatException>(() =>
            new Zip64DirectoryEndHeader().Read(new BinaryReader(stream))
        );
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await new Zip64DirectoryEndHeader().Read(new AsyncBinaryReader(stream))
        );
    }

    [Theory]
    [InlineData(44UL, 0)]
    [InlineData(10044UL, 10000)]
    public async Task Zip64_Skips_Extensible_Data_Exactly(ulong size, int payloadSize)
    {
        using var stream = CreateRecord(size, payloadSize);
        new Zip64DirectoryEndHeader().Read(new BinaryReader(stream));
        Assert.Equal(stream.Length, stream.Position);
        stream.Position = 0;
        await new Zip64DirectoryEndHeader().Read(new AsyncBinaryReader(stream));
        Assert.Equal(stream.Length, stream.Position);
    }

    [Theory]
    [InlineData(2147483691UL)]
    [InlineData(4294967340UL)]
    public async Task Zip64_Rejects_Huge_Truncated_Extensible_Data(ulong size)
    {
        using var stream = CreateRecord(size, 1);
        Assert.ThrowsAny<SharpCompressException>(() =>
            new Zip64DirectoryEndHeader().Read(new BinaryReader(stream))
        );
        stream.Position = 0;
        await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
            await new Zip64DirectoryEndHeader().Read(new AsyncBinaryReader(stream))
        );
    }

    private static MemoryStream CreateRecord(ulong size, int payloadSize)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(size);
            writer.Write(new byte[44 + payloadSize]);
        }
        stream.Position = 0;
        return stream;
    }
}
