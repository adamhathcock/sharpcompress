using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Lzw;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzwStreamAsyncTests : TestBase
{
    [Fact]
    public async Task LzwStream_ReadAsync_ByteArray()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");
        using var stream = File.OpenRead(testArchive);
        using var lzwStream = new LzwStream(stream);

        var buffer = new byte[4096];
        int bytesRead = await lzwStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

        Assert.True(bytesRead > 0, "Should read at least some data");
    }

#if !LEGACY_DOTNET
    [Fact]
    public async Task LzwStream_ReadAsync_Memory()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");
        using var stream = File.OpenRead(testArchive);
        using var lzwStream = new LzwStream(stream);

        var buffer = new byte[4096];
        int bytesRead = await lzwStream.ReadAsync(new Memory<byte>(buffer)).ConfigureAwait(false);

        Assert.True(bytesRead > 0, "Should read at least some data");
    }
#endif

    [Fact]
    public async Task LzwStream_ReadAsync_ProducesSameResultAsSync()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");

        byte[] syncResult;
        byte[] asyncResult;

        using (var stream = File.OpenRead(testArchive))
        using (var lzwStream = new LzwStream(stream))
        {
            syncResult = ReadAllSync(lzwStream);
        }

        using (var stream = File.OpenRead(testArchive))
        using (var lzwStream = new LzwStream(stream))
        {
            asyncResult = await ReadAllAsync(lzwStream).ConfigureAwait(false);
        }

        Assert.Equal(syncResult, asyncResult);
    }

    [Fact]
    public async Task LzwStream_ReadAsync_MultipleReads()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");
        using var stream = File.OpenRead(testArchive);
        using var lzwStream = new LzwStream(stream);

        var totalData = new List<byte>();
        var buffer = new byte[1024];
        int bytesRead;

        while (
            (bytesRead = await lzwStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false))
            > 0
        )
        {
            for (int i = 0; i < bytesRead; i++)
            {
                totalData.Add(buffer[i]);
            }
        }

        Assert.True(totalData.Count > 0, "Should have read some data");
    }

    [Fact]
    public async Task LzwStream_ReadAsync_Cancellation()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");
        using var stream = File.OpenRead(testArchive);
        using var lzwStream = new LzwStream(stream);

        var cts = new CancellationTokenSource();
        var buffer = new byte[4096];

        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await lzwStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)
        );
    }

    [Fact]
    public async Task LzwStream_ReadAsync_EmptyBuffer()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");
        using var stream = File.OpenRead(testArchive);
        using var lzwStream = new LzwStream(stream);

        var buffer = Array.Empty<byte>();
        int bytesRead = await lzwStream.ReadAsync(buffer, 0, 0).ConfigureAwait(false);

        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async Task LzwStream_ReadAsync_ReturnsZeroAtEndOfStream()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z");
        using var stream = File.OpenRead(testArchive);
        using var lzwStream = new LzwStream(stream);

        var buffer = new byte[4096];

        int bytesRead;
        while (
            (bytesRead = await lzwStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false))
            > 0
        ) { }

        Assert.Equal(0, bytesRead);

        bytesRead = await lzwStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    private static async Task<byte[]> ReadAllAsync(LzwStream stream)
    {
        var result = new List<byte>();
        var buffer = new byte[4096];
        int bytesRead;

        while (
            (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0
        )
        {
            for (int i = 0; i < bytesRead; i++)
            {
                result.Add(buffer[i]);
            }
        }

        return result.ToArray();
    }

    private static byte[] ReadAllSync(LzwStream stream)
    {
        var result = new List<byte>();
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                result.Add(buffer[i]);
            }
        }

        return result.ToArray();
    }
}
