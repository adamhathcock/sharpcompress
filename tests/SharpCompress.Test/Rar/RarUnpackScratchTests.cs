using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Compressors.Rar;
using SharpCompress.Compressors.Rar.UnpackV2017;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarUnpackScratchTests : TestBase
{
    [Theory]
    [InlineData("Rar5.rar")]
    [InlineData("Rar5.solid.rar")]
    [InlineData("Rar5.crc_blake2.rar")]
    public async Task Rar_Async_Reuses_Huffman_Scratch_Between_Entries(string archiveName)
    {
        using var source = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archiveName));
        await using var archive = await RarArchive.OpenAsyncArchive(
            source,
            ReaderOptions.ForExternalStream,
            CancellationToken.None
        );
        byte[]? bitLengths = null;
        byte[]? lengths = null;
        var entryCount = 0;
        await foreach (var entry in archive.EntriesAsync)
        {
            if (entry.IsDirectory)
            {
                continue;
            }
            await entry.WriteToAsync(Stream.Null, cancellationToken: CancellationToken.None);
            var unpack = ((RarArchive)archive).UnpackV2017.Value;
            var currentBitLengths = GetScratch(unpack, "tableReadBitLengths");
            var currentLengths = GetScratch(unpack, "tableReadLengths");
            Assert.NotEmpty(currentBitLengths);
            Assert.NotEmpty(currentLengths);
            if (bitLengths is not null)
            {
                Assert.Same(bitLengths, currentBitLengths);
                Assert.Same(lengths, currentLengths);
            }
            bitLengths = currentBitLengths;
            lengths = currentLengths;
            // Reused buffers must not depend on the zero initialization of new arrays.
            currentBitLengths.AsSpan().Fill(0xCC);
            currentLengths.AsSpan().Fill(0xCC);
            entryCount++;
        }
        Assert.True(entryCount > 1);
        await archive.DisposeAsync();
        Assert.True(source.CanRead);
        Assert.Empty(GetScratch(((RarArchive)archive).UnpackV2017.Value, "tableReadBitLengths"));
        Assert.Empty(GetScratch(((RarArchive)archive).UnpackV2017.Value, "tableReadLengths"));
    }

    [Theory]
    [InlineData(false)]
#if !LEGACY_DOTNET
    [InlineData(true)]
#endif
    public async Task Rar_Async_Releases_Huffman_Scratch_After_Failure(bool cancel)
    {
        using var source = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.rar"));
        var archive = await RarArchive.OpenAsyncArchive(
            source,
            ReaderOptions.ForExternalStream,
            CancellationToken.None
        );
        await using (archive)
        {
            var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
            using var input = await entry.OpenEntryStreamAsync(CancellationToken.None);
            var buffer = new byte[1024];
            Assert.True(
                await input.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None) > 0
            );
            var unpack = ((RarArchive)archive).UnpackV2017.Value;
            Assert.NotEmpty(GetScratch(unpack, "tableReadLengths"));
            if (cancel)
            {
#if !LEGACY_DOTNET
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await input.ReadAsync(buffer.AsMemory(), cancellation.Token)
                );
#endif
            }
            else
            {
                ((RarArchiveEntry)entry).FileHeader.FileCrc.NotNull()[0] ^= 0xFF;
                await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                    await input.CopyToAsync(Stream.Null, 81920, CancellationToken.None)
                );
            }
        }
        await archive.DisposeAsync();
        Assert.Empty(GetScratch(((RarArchive)archive).UnpackV2017.Value, "tableReadBitLengths"));
        Assert.Empty(GetScratch(((RarArchive)archive).UnpackV2017.Value, "tableReadLengths"));
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task Rar5_Async_Zero_Run_Uses_Alphabet_Size_Not_Rented_Length()
    {
        using var unpack = new Unpack();
        var bitLengths = ArrayPool<byte>.Shared.Rent((int)PackDef.BC);
        bitLengths.AsSpan().Fill(0xCC);
        GetField("tableReadBitLengths").SetValue(unpack, bitLengths);
        GetField("BlockHeader").SetValue(unpack, new UnpackBlockHeader { TablePresent = true });
        using var input = new BitInput(false) { ExternalBuffer = true, InBuf = new byte[4096] };
        // Two runs of 17 zeros exceed the 20-symbol alphabet, but not the rented array.
        input.InBuf[0] = 0xFF;
        input.InBuf[1] = 0xFF;
        var readTables = typeof(Unpack).GetMethod(
            "ReadTablesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        Assert.True(
            await (ValueTask<bool>)readTables.Invoke(unpack, [input, CancellationToken.None])!
        );
        for (var i = (int)PackDef.BC; i < bitLengths.Length; i++)
        {
            Assert.Equal(0xCC, bitLengths[i]);
        }
    }

    [Fact]
    public async Task Rar2_Async_Huffman_History_Clears_Unused_Pooled_Tail()
    {
        using var unpack = new Unpack();
        var lengths = ArrayPool<byte>.Shared.Rent(checked((int)PackDef.MC20 * 4));
        lengths.AsSpan().Fill(0xCC);
        GetField("tableReadLengths").SetValue(unpack, lengths);
        // Zero code lengths decode a non-audio table, leaving the audio-history tail unused.
        unpack.InBuf.AsSpan().Clear();
        GetField("ReadTop").SetValue(unpack, 4096);
        var readTables = typeof(Unpack).GetMethod(
            "ReadTables20Async",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        Assert.True(await (ValueTask<bool>)readTables.Invoke(unpack, [CancellationToken.None])!);
        Assert.All(GetScratch(unpack, "UnpOldTable20"), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar_Reader_Async_Dispose_Releases_Huffman_Scratch(bool leaveStreamOpen)
    {
        using var source = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar"))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            source,
            new ReaderOptions { LeaveStreamOpen = leaveStreamOpen },
            CancellationToken.None
        );
        while (await reader.MoveToNextEntryAsync(CancellationToken.None))
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToAsync(Stream.Null, CancellationToken.None);
            }
        }
        var unpack = (
            (Lazy<IRarUnpack>)
                typeof(RarReader)
                    .GetProperty("UnpackV2017", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(reader)!
        ).Value;
        Assert.NotEmpty(GetScratch(unpack, "tableReadLengths"));
        await reader.DisposeAsync();
        await reader.DisposeAsync();
        Assert.Empty(GetScratch(unpack, "tableReadBitLengths"));
        Assert.Empty(GetScratch(unpack, "tableReadLengths"));
        Assert.Equal(leaveStreamOpen, source.CanRead);
    }

    private static FieldInfo GetField(string name) =>
        typeof(Unpack).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static byte[] GetScratch(object unpack, string name) =>
        (byte[])GetField(name).GetValue(unpack)!;
}
