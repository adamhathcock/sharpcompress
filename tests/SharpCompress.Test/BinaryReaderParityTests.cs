using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test;

public class BinaryReaderParityTests : TestBase
{
    private readonly byte[] _testData;

    public BinaryReaderParityTests()
    {
        // Create test data with various patterns
        _testData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            _testData[i] = (byte)i;
        }
    }

    [Fact]
    public async Task ReadByte_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        for (int i = 0; i < 10; i++)
        {
            byte syncByte = syncReader.ReadByte();
            byte asyncByte = await asyncReader.ReadByteAsync();
            Assert.Equal(syncByte, asyncByte);
        }
    }

    [Fact]
    public async Task ReadBytes_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        var syncBytes = syncReader.ReadBytes(32);
        var asyncBuffer = new byte[32];
        await asyncReader.ReadBytesAsync(asyncBuffer, 0, 32);

        Assert.Equal(syncBytes, asyncBuffer);
    }

    [Fact]
    public async Task ReadUInt16_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        ushort syncValue = syncReader.ReadUInt16();
        ushort asyncValue = await asyncReader.ReadUInt16Async();

        Assert.Equal(syncValue, asyncValue);
    }

    [Fact]
    public async Task ReadUInt32_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        uint syncValue = syncReader.ReadUInt32();
        uint asyncValue = await asyncReader.ReadUInt32Async();

        Assert.Equal(syncValue, asyncValue);
    }

    [Fact]
    public async Task ReadUInt64_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        ulong syncValue = syncReader.ReadUInt64();
        ulong asyncValue = await asyncReader.ReadUInt64Async();

        Assert.Equal(syncValue, asyncValue);
    }

    [Fact]
    public void Position_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        // Read some bytes
        syncReader.ReadBytes(10);
        var asyncBuffer = new byte[10];
        asyncReader.ReadBytesAsync(asyncBuffer, 0, 10).AsTask().Wait();

        Assert.Equal(syncStream.Position, asyncStream.Position);
        Assert.Equal(syncReader.BaseStream.Position, asyncReader.BaseStream.Position);
    }

    [Fact]
    public async Task MultipleReads_Parity()
    {
        using var syncStream = new MemoryStream(_testData);
        using var asyncStream = new MemoryStream(_testData);
        using var syncReader = new BinaryReader(syncStream);
        using var asyncReader = new AsyncBinaryReader(asyncStream);

        // Mix of different read operations
        Assert.Equal(syncReader.ReadByte(), await asyncReader.ReadByteAsync());

        var syncBytes = new byte[16];
        var asyncBytes = new byte[16];
        syncStream.Read(syncBytes, 0, 16);
        await asyncReader.ReadBytesAsync(asyncBytes, 0, 16);
        Assert.Equal(syncBytes, asyncBytes);

        Assert.Equal(syncReader.ReadUInt16(), await asyncReader.ReadUInt16Async());
        Assert.Equal(syncReader.ReadUInt32(), await asyncReader.ReadUInt32Async());
        Assert.Equal(syncReader.ReadUInt64(), await asyncReader.ReadUInt64Async());
    }

    [Fact]
    public async Task ReadByte_Async_Properly_Async()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new AsyncBinaryReader(stream);

        var bytes = new byte[10];
        for (int i = 0; i < 10; i++)
        {
            bytes[i] = await reader.ReadByteAsync();
        }

        Assert.Equal(_testData.Take(10).ToArray(), bytes);
    }

    [Fact]
    public async Task ReadBytes_Async_Properly_Async()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new AsyncBinaryReader(stream);

        var buffer = new byte[64];
        await reader.ReadBytesAsync(buffer, 0, 64);

        Assert.Equal(_testData.Take(64).ToArray(), buffer);
    }

    [Fact]
    public async Task SkipAsync_Moves_Position_Correctly()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new AsyncBinaryReader(stream);

        await reader.SkipAsync(10);

        Assert.Equal(10, stream.Position);
        Assert.Equal(10, reader.BaseStream.Position);
    }

    [Fact]
    public async Task SkipAsync_Then_Read_Works_Correctly()
    {
        using var stream = new MemoryStream(_testData);
        using var reader = new AsyncBinaryReader(stream);

        await reader.SkipAsync(10);
        byte b = await reader.ReadByteAsync();

        Assert.Equal(_testData[10], b);
    }
}
