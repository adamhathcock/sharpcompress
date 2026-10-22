using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.SevenZip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.SevenZip;

/// <summary>
/// Unit tests for the bookkeeping of the shared solid-folder decoder.
/// </summary>
public class FolderStreamTrackerTests
{
    private static byte[] Payload(int size)
    {
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = (byte)(i % 251);
        }
        return data;
    }

    [Fact]
    public void Reads_AdvanceTheTrackedPosition()
    {
        var payload = Payload(64);
        var tracker = new FolderStreamTracker(new MemoryStream(payload));
        Assert.Equal(0, tracker.Position);

        var buffer = new byte[10];
        Assert.Equal(10, tracker.Read(buffer, 0, buffer.Length));
        Assert.Equal(10, tracker.Position);

        Assert.Equal(payload[10], tracker.ReadByte());
        Assert.Equal(11, tracker.Position);
    }

    [Fact]
    public async Task AsyncReads_AdvanceTheTrackedPosition()
    {
        var tracker = new FolderStreamTracker(new MemoryStream(Payload(64)));
        var buffer = new byte[10];

        Assert.Equal(10, await tracker.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None));
        Assert.Equal(10, tracker.Position);
    }

    [Fact]
    public void Skip_OverAForwardOnlyStream_ReadsTheSkippedBytes()
    {
        var payload = Payload(64);
        var tracker = new FolderStreamTracker(new ForwardOnlyStream(new MemoryStream(payload)));
        Assert.False(tracker.CanSeek);

        tracker.Skip(16);
        Assert.Equal(16, tracker.Position);

        var buffer = new byte[4];
        Assert.Equal(4, tracker.Read(buffer, 0, buffer.Length));
        Assert.Equal(payload.Skip(16).Take(4), buffer);
        Assert.Equal(20, tracker.Position);
    }

    [Fact]
    public void Skip_OverASeekableStream_SeeksAndKeepsThePosition()
    {
        // The parallel decode path hands out a seekable temp file instead of a decoder.
        var payload = Payload(64);
        var tracker = new FolderStreamTracker(new MemoryStream(payload));
        Assert.True(tracker.CanSeek);

        tracker.Skip(16);
        Assert.Equal(16, tracker.Position);

        var buffer = new byte[4];
        Assert.Equal(4, tracker.Read(buffer, 0, buffer.Length));
        Assert.Equal(payload.Skip(16).Take(4), buffer);
        Assert.Equal(20, tracker.Position);
    }

    [Fact]
    public async Task MixingReadModes_KeepsTheDecoderInASingleMode()
    {
        var payload = Payload(64);
        var tracker = new FolderStreamTracker(new MemoryStream(payload));
        var buffer = new byte[10];

        Assert.Equal(10, tracker.Read(buffer, 0, buffer.Length));

        // An asynchronous read after a synchronous one must not fail and must keep counting.
        Assert.Equal(10, await tracker.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None));
        Assert.Equal(20, tracker.Position);

        // ... and the other way around.
        var other = new FolderStreamTracker(new MemoryStream(payload));
        Assert.Equal(10, await other.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None));
        Assert.Equal(10, other.Read(buffer, 0, buffer.Length));
        Assert.Equal(20, other.Position);
    }
}
