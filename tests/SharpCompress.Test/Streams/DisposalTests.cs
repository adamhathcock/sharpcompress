using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.PPMd;
using SharpCompress.Compressors.Reduce;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DisposalTests
{
    private void VerifyStreamDisposal(
        Func<Stream, bool, Stream> createStream,
        bool supportsLeaveOpen = true
    )
    {
        // 1. Test Dispose behavior (should dispose inner stream)
        {
            using var innerStream = new TestStream(new MemoryStream());
            // createStream(stream, leaveOpen: false)
            var stream = createStream(innerStream, false);
            stream.Dispose();

            // Some streams might not support disposal of inner stream (e.g. PpmdStream apparently)
            // But for those that satisfy the pattern, we assert true.
            Assert.True(
                innerStream.IsDisposed,
                "Stream should have been disposed when leaveOpen=false"
            );
        }

        // 2. Test LeaveOpen behavior (should NOT dispose inner stream)
        if (supportsLeaveOpen)
        {
            using var innerStream = new TestStream(new MemoryStream());
            // createStream(stream, leaveOpen: true)
            var stream = createStream(innerStream, true);
            stream.Dispose();
            Assert.False(
                innerStream.IsDisposed,
                "Stream should NOT have been disposed when leaveOpen=true"
            );
        }
    }

    private void VerifyAlwaysDispose(Func<Stream, Stream> createStream)
    {
        using var innerStream = new TestStream(new MemoryStream());
        var stream = createStream(innerStream);
        stream.Dispose();
        Assert.True(innerStream.IsDisposed, "Stream should have been disposed (AlwaysDispose)");
    }

    private void VerifyNeverDispose(Func<Stream, Stream> createStream)
    {
        using var innerStream = new TestStream(new MemoryStream());
        var stream = createStream(innerStream);
        stream.Dispose();
        Assert.False(innerStream.IsDisposed, "Stream should NOT have been disposed (NeverDispose)");
    }

    [Fact]
    public void SourceStream_Disposal()
    {
        VerifyStreamDisposal(
            (stream, leaveOpen) =>
                new SourceStream(
                    stream,
                    i => null,
                    new ReaderOptions { LeaveStreamOpen = leaveOpen }
                )
        );
    }

    [Fact]
    public void ProgressReportingStream_Disposal()
    {
        VerifyStreamDisposal(
            (stream, leaveOpen) =>
                new ProgressReportingStream(
                    stream,
                    new Progress<ProgressReport>(),
                    "",
                    0,
                    leaveOpen: leaveOpen
                )
        );
    }

    [Fact]
    public void DataDescriptorStream_Disposal()
    {
        // DataDescriptorStream DOES dispose inner stream
        VerifyAlwaysDispose(stream => new DataDescriptorStream(stream));
    }

    [Fact]
    public void DeflateStream_Disposal()
    {
        // DeflateStream in SharpCompress always disposes inner stream
        VerifyAlwaysDispose(stream => new DeflateStream(stream, CompressionMode.Compress));
    }

    [Fact]
    public void GZipStream_Disposal()
    {
        // GZipStream in SharpCompress always disposes inner stream
        VerifyAlwaysDispose(stream => new GZipStream(stream, CompressionMode.Compress));
    }

    [Fact]
    public void LzwStream_Disposal()
    {
        VerifyStreamDisposal(
            (stream, leaveOpen) =>
            {
                var lzw = new LzwStream(stream);
                lzw.IsStreamOwner = !leaveOpen;
                return lzw;
            }
        );
    }

    [Fact]
    public void PpmdStream_Disposal()
    {
        // PpmdStream seems to not dispose inner stream based on code analysis
        // It takes PpmdProperties which we need to mock or create.
        var props = new PpmdProperties();
        VerifyNeverDispose(stream => new PpmdStream(props, stream, false));
    }

    [Fact]
    public void LzmaStream_Disposal()
    {
        // LzmaStream always disposes inner stream
        // Need to provide valid properties to avoid crash in constructor (invalid window size)
        // 5 bytes: 1 byte properties + 4 bytes dictionary size (little endian)
        // Dictionary size = 1024 (0x400) -> 00 04 00 00
        var lzmaProps = new byte[] { 0, 0, 4, 0, 0 };
        VerifyAlwaysDispose(stream => new LzmaStream(lzmaProps, stream));
    }

    [Fact]
    public void LZipStream_Disposal()
    {
        // LZipStream always disposes inner stream
        // Use Compress mode to avoid need for valid input header
        VerifyAlwaysDispose(stream => new LZipStream(stream, CompressionMode.Compress));
    }

    [Fact]
    public void ReduceStream_Disposal()
    {
        // ReduceStream does not dispose inner stream
        VerifyNeverDispose(stream => new ReduceStream(stream, 0, 0, 1));
    }

    [Fact]
    public void ZStandard_CompressionStream_Disposal()
    {
        VerifyStreamDisposal(
            (stream, leaveOpen) =>
                new CompressionStream(stream, level: 0, bufferSize: 0, leaveOpen: leaveOpen)
        );
    }

    [Fact]
    public void ZStandard_DecompressionStream_Disposal()
    {
        VerifyStreamDisposal(
            (stream, leaveOpen) =>
                new DecompressionStream(
                    stream,
                    bufferSize: 0,
                    checkEndOfStream: false,
                    leaveOpen: leaveOpen
                )
        );
    }
}
