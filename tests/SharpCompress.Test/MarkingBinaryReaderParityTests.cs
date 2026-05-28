using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test;

public class MarkingBinaryReaderParityTests : TestBase
{
    private readonly byte[] _testData;

    public MarkingBinaryReaderParityTests()
    {
        // Create test data with various patterns
        _testData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            _testData[i] = (byte)i;
        }
    }

    [Fact]
    public void Mark_Resets_ByteCount()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new MarkingBinaryReader(stream);

        reader.ReadBytes(10);
        Assert.Equal(10, reader.CurrentReadByteCount);

        reader.Mark();
        Assert.Equal(0, reader.CurrentReadByteCount);

        reader.ReadBytes(5);
        Assert.Equal(5, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task Mark_Resets_ByteCount_Async()
    {
        using var stream = new MemoryStream(_testData);
        var reader = new AsyncMarkingBinaryReader(stream);

        await reader.ReadBytesAsync(10);
        Assert.Equal(10, reader.CurrentReadByteCount);

        reader.Mark();
        Assert.Equal(0, reader.CurrentReadByteCount);

        await reader.ReadBytesAsync(5);
        Assert.Equal(5, reader.CurrentReadByteCount);
    }

    [Fact]
    public void ReadByte_Updates_ByteCount()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new MarkingBinaryReader(stream);

        reader.Mark();
        reader.ReadByte();
        Assert.Equal(1, reader.CurrentReadByteCount);

        reader.ReadByte();
        Assert.Equal(2, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task ReadByte_Updates_ByteCount_Async()
    {
        using var stream = new MemoryStream(_testData);
        var reader = new AsyncMarkingBinaryReader(stream);

        reader.Mark();
        await reader.ReadByteAsync();
        Assert.Equal(1, reader.CurrentReadByteCount);

        await reader.ReadByteAsync();
        Assert.Equal(2, reader.CurrentReadByteCount);
    }

    [Fact]
    public void ReadBytes_Updates_ByteCount()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new MarkingBinaryReader(stream);

        reader.Mark();
        reader.ReadBytes(16);
        Assert.Equal(16, reader.CurrentReadByteCount);

        reader.ReadBytes(8);
        Assert.Equal(24, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task ReadBytes_Updates_ByteCount_Async()
    {
        using var stream = new MemoryStream(_testData);
        var reader = new AsyncMarkingBinaryReader(stream);

        reader.Mark();
        await reader.ReadBytesAsync(16);
        Assert.Equal(16, reader.CurrentReadByteCount);

        await reader.ReadBytesAsync(8);
        Assert.Equal(24, reader.CurrentReadByteCount);
    }

    [Fact]
    public void ReadUInt16_Updates_ByteCount()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new MarkingBinaryReader(stream);

        reader.Mark();
        reader.ReadUInt16();
        Assert.Equal(2, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task ReadUInt16_Updates_ByteCount_Async()
    {
        using var stream = new MemoryStream(_testData);
        var reader = new AsyncMarkingBinaryReader(stream);

        reader.Mark();
        await reader.ReadUInt16Async();
        Assert.Equal(2, reader.CurrentReadByteCount);
    }

    [Fact]
    public void ReadUInt32_Updates_ByteCount()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new MarkingBinaryReader(stream);

        reader.Mark();
        reader.ReadUInt32();
        Assert.Equal(4, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task ReadUInt32_Updates_ByteCount_Async()
    {
        using var stream = new MemoryStream(_testData);
        var reader = new AsyncMarkingBinaryReader(stream);

        reader.Mark();
        await reader.ReadUInt32Async();
        Assert.Equal(4, reader.CurrentReadByteCount);
    }

    [Fact]
    public void ReadRarVInt_Updates_ByteCount()
    {
        // Create valid RAR v-int data: 0x05 (value 5, no continuation bit)
        var data = new byte[] { 0x05, 0x85, 0x01, 0x00 }; // 0x05, then 0x85 0x01 (value 5 + 128 = 133)
        using var stream = new MemoryStream(data);
        using var reader = new MarkingBinaryReader(stream);

        reader.Mark();
        // Read a single-byte v-int (value 5, no continuation bit)
        reader.ReadRarVInt();
        Assert.Equal(1, reader.CurrentReadByteCount);

        reader.Mark();
        // Read a two-byte v-int (0x85 means continuation, then 0x01)
        reader.ReadRarVInt();
        Assert.Equal(2, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task ReadRarVInt_Updates_ByteCount_Async()
    {
        // Create valid RAR v-int data: 0x05 (value 5, no continuation bit)
        var data = new byte[] { 0x05, 0x85, 0x01, 0x00 };
        using var stream = new MemoryStream(data);
        var reader = new AsyncMarkingBinaryReader(stream);

        reader.Mark();
        // Read a single-byte v-int (value 5, no continuation bit)
        await reader.ReadRarVIntAsync();
        Assert.Equal(1, reader.CurrentReadByteCount);

        reader.Mark();
        // Read a two-byte v-int (0x85 means continuation, then 0x01)
        await reader.ReadRarVIntAsync();
        Assert.Equal(2, reader.CurrentReadByteCount);
    }

    [Fact]
    public async Task Sync_Async_ByteCount_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new MarkingBinaryReader(syncStream);
        var asyncReader = new AsyncMarkingBinaryReader(asyncStream);

        syncReader.Mark();
        asyncReader.Mark();

        // Read bytes with sync
        syncReader.ReadByte();
        syncReader.ReadByte();
        syncReader.ReadUInt16();
        syncReader.ReadUInt32();
        syncReader.ReadBytes(8);
        var syncCount = syncReader.CurrentReadByteCount;

        // Read bytes with async
        await asyncReader.ReadByteAsync();
        await asyncReader.ReadByteAsync();
        await asyncReader.ReadUInt16Async();
        await asyncReader.ReadUInt32Async();
        await asyncReader.ReadBytesAsync(8);
        var asyncCount = asyncReader.CurrentReadByteCount;

        Assert.Equal(syncCount, asyncCount);
        Assert.Equal(16, syncCount);
        Assert.Equal(16, asyncCount);
    }

    [Fact]
    public async Task Sync_Async_ByteCount_Parity_Alt()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new MarkingBinaryReader(syncStream);
        var asyncReader = new AsyncMarkingBinaryReader(asyncStream);

        syncReader.Mark();
        asyncReader.Mark();

        // Read bytes with sync
        syncReader.ReadByte();
        syncReader.ReadByte();
        syncReader.ReadUInt16();
        syncReader.ReadUInt32();
        syncReader.ReadBytes(8);
        var syncCount = syncReader.CurrentReadByteCount;

        // Read bytes with async
        await asyncReader.ReadByteAsync();
        await asyncReader.ReadByteAsync();
        await asyncReader.ReadUInt16Async();
        await asyncReader.ReadUInt32Async();
        await asyncReader.ReadBytesAsync(8);
        var asyncCount = asyncReader.CurrentReadByteCount;

        Assert.Equal(syncCount, asyncCount);
        Assert.Equal(16, syncCount);
        Assert.Equal(16, asyncCount);
    }
}
