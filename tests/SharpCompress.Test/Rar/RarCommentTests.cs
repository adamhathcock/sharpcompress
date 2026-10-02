using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common.Rar;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarCommentTests : TestBase
{
    public static IEnumerable<object[]> Comments()
    {
        // Generated with RAR 7.22: rar a -ma5 -m0 -qo- -zcomment.txt [-hptest|-ptest] archive.rar payload.txt.
        foreach (var useAsync in new[] { false, true })
        {
            yield return ["Rar5.comment.encrypted15.rar", "123456789012345", useAsync];
            yield return ["Rar5.comment.encrypted16.rar", "1234567890123456", useAsync];
            yield return ["Rar5.comment.encrypted17.rar", "123456789012345é", useAsync];
            yield return ["Rar5.comment.plain.rar", "123456789012345é", useAsync];
            yield return ["Rar5.comment.filesOnly.rar", "123456789012345é", useAsync];
        }
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public async Task Rar5_Comment_Archive(string filename, string expected, bool useAsync)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        var options = ReaderOptions.ForExternalStream.WithPassword("test");
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var archive = await RarArchive.OpenAsyncArchive(stream, options);
            await foreach (var entry in archive.EntriesAsync)
            {
                Assert.Equal("payload.txt", entry.Key);
                await entry.WriteToAsync(output, cancellationToken: CancellationToken.None);
            }
            await foreach (var volume in archive.VolumesAsync)
            {
                Assert.Equal(expected, ((RarVolume)volume).Comment);
            }
        }
        else
        {
            using var archive = RarArchive.OpenArchive(stream, options);
            var entry = Assert.Single(archive.Entries);
            Assert.Equal("payload.txt", entry.Key);
            entry.WriteTo(output);
            Assert.Equal(expected, archive.Volumes.OfType<RarVolume>().First().Comment);
        }
        Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("wrong", false)]
    [InlineData("wrong", true)]
    public async Task Rar5_Comment_Rejects_Invalid_Password(string? password, bool useAsync)
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.comment.encrypted17.rar")
        );
        var options = ReaderOptions.ForExternalStream with { Password = password };
        if (useAsync)
        {
            await Assert.ThrowsAsync<SharpCompress.Common.CryptographicException>(async () =>
            {
                await using var archive = await RarArchive.OpenAsyncArchive(stream, options);
                await foreach (var entry in archive.EntriesAsync)
                {
                    _ = entry.Key;
                }
            });
        }
        else
        {
            Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            {
                using var archive = RarArchive.OpenArchive(stream, options);
                _ = archive.Entries.ToArray();
            });
        }
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public async Task Rar5_Comment_Reader(string filename, string expected, bool useAsync)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var forwardOnly = new ForwardOnlyStream(stream);
        var options = ReaderOptions.ForExternalStream.WithPassword("test");
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var reader = await RarReader.OpenAsyncReader(forwardOnly, options);
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(expected, ((RarReader)reader).Volume!.Comment);
            await reader.WriteEntryToAsync(output, CancellationToken.None);
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = RarReader.OpenReader(forwardOnly, options);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(expected, ((RarReader)reader).Volume!.Comment);
            reader.WriteEntryTo(output);
            Assert.False(reader.MoveToNextEntry());
        }
        Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(forwardOnly.CanRead);
    }

    [Fact]
    public async Task Rar5_Comment_Volume_Async_Cancellation()
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.comment.encrypted17.rar")
        );
        using var volume = new StreamRarArchiveVolume(
            stream,
            ReaderOptions.ForExternalStream.WithPassword("test"),
            0
        );
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var part in volume.GetVolumeFilePartsAsync(cancellation.Token))
            {
                _ = part.FileHeader;
            }
        });
    }
}
