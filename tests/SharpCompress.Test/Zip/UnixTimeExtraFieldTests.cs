using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class UnixTimeExtraFieldTests
{
    private const int ModifiedSeconds = 0x65E72A53; // 2024-03-05 14:21:07 UTC
    private const int AccessedSeconds = 0x65E7C08B; // 2024-03-06 01:02:03 UTC

    private static readonly DateTime Modified = new(2024, 3, 5, 14, 21, 7, DateTimeKind.Utc);
    private static readonly DateTime Accessed = new(2024, 3, 6, 1, 2, 3, DateTimeKind.Utc);

    // Info-ZIP Zip 3.0 (TZ=UTC): "zip -0 ut.zip a.txt" with mtime 2024-03-05 14:21:07
    // and atime 2024-03-06 01:02:03. The local header has "UT" with flags 0x03 and
    // both times; the central directory has flags 0x03 and only the modification time.
    private static readonly byte[] InfoZipArchive = FromHex(
        "504B03040A0000000000A472655886A61036050000000500000005001C00612E"
            + "7478745554090003532AE7658BC0E76575780B00010400000000040000000068"
            + "656C6C6F504B01021E030A0000000000A472655886A610360500000005000000"
            + "050018000000000000000000FF8100000000612E7478745554050003532AE765"
            + "75780B000104000000000400000000504B050600000000010001004B00000044"
            + "0000000000"
    );

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x03)]
    [InlineData(0x07)]
    public void Central_Directory_Form_Keeps_The_Modification_Time(byte flags)
    {
        // The central directory stores only the modification time; the flags
        // still describe the local header.
        var times = CreateField(flags, ModifiedSeconds).UnicodeTimes;

        Assert.Equal(Modified, times.Item1);
        Assert.Null(times.Item2);
        Assert.Null(times.Item3);
    }

    [Fact]
    public void Local_Header_Form_Reads_All_Flagged_Times()
    {
        var times = CreateField(0x03, ModifiedSeconds, AccessedSeconds).UnicodeTimes;

        Assert.Equal(Modified, times.Item1);
        Assert.Equal(Accessed, times.Item2);
        Assert.Null(times.Item3);
    }

    [Fact]
    public void Zip_Archive_And_Reader_Use_The_Same_Unix_Modification_Time()
    {
        var local = Modified.ToLocalTime();
        var expected = new DateTime(
            local.Year,
            local.Month,
            local.Day,
            local.Hour,
            local.Minute,
            local.Second / 2 * 2
        );

        using (var archive = ZipArchive.OpenArchive(new MemoryStream(InfoZipArchive)))
        {
            Assert.Equal(expected, archive.Entries.Single().LastModifiedTime);
        }

        using var reader = ZipReader.OpenReader(
            new MemoryStream(InfoZipArchive),
            ReaderOptions.ForExternalStream
        );
        Assert.True(reader.MoveToNextEntry());
        Assert.Equal(expected, reader.Entry.LastModifiedTime);
    }

    private static UnixTimeExtraField CreateField(byte flags, params int[] seconds)
    {
        var data = new byte[1 + (4 * seconds.Length)];
        data[0] = flags;
        for (var i = 0; i < seconds.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1 + (4 * i)), seconds[i]);
        }

        return new UnixTimeExtraField(ExtraDataType.UnixTimeExtraField, (ushort)data.Length, data);
    }

    private static byte[] FromHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }
}
