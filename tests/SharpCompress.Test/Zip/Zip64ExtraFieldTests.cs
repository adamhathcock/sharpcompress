using System.Buffers.Binary;
using SharpCompress.Common;
using SharpCompress.Common.Zip.Headers;
using Xunit;

namespace SharpCompress.Test.Zip;

public class Zip64ExtraFieldTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void Zip64_Extra_Field_Accepts_Supported_Sizes(long size)
    {
        var field = CreateExtraField((ulong)size);

        field.Process(0, uint.MaxValue, 0, 0);
        Assert.Equal(size, field.CompressedSize);

        field.Process(uint.MaxValue, 0, 0, 0);
        Assert.Equal(size, field.UncompressedSize);
    }

    [Theory]
    [InlineData((ulong)long.MaxValue + 1)]
    [InlineData(ulong.MaxValue)]
    public void Zip64_Extra_Field_Rejects_Out_Of_Range_Sizes(ulong size)
    {
        var field = CreateExtraField(size);

        Assert.Throws<ArchiveException>(() => field.Process(0, uint.MaxValue, 0, 0));
        Assert.Throws<ArchiveException>(() => field.Process(uint.MaxValue, 0, 0, 0));
    }

    private static Zip64ExtendedInformationExtraField CreateExtraField(ulong size)
    {
        var data = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(data, size);
        return new Zip64ExtendedInformationExtraField(
            ExtraDataType.Zip64ExtendedInformationExtraField,
            (ushort)data.Length,
            data
        );
    }
}
