using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Streams;

public class TarLifetimeTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reader_Preserves_Existing_Recording_Session(bool async)
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        using var input = new ForwardOnlyStream(file);
        using var buffered = SharpCompressStream.Create(input, TarWrapper.MaximumRewindBufferSize);
        buffered.StartRecording();
        var options = new ReaderOptions { LeaveStreamOpen = false };
        if (async)
        {
            await using var reader = await TarReader.OpenAsyncReader(
                buffered,
                options,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = TarReader.OpenReader(buffered, options);
            Assert.True(reader.MoveToNextEntry());
        }
    }

    public static IEnumerable<object[]> FrozenRecordingCases()
    {
        foreach (var async in new[] { false, true })
        {
            foreach (var stopRecording in new[] { false, true })
            {
                foreach (var leaveOpen in new[] { false, true })
                {
                    foreach (var seekable in new[] { false, true })
                    {
                        foreach (var factory in new[] { 1, 2 })
                        {
                            yield return [async, stopRecording, leaveOpen, seekable, factory];
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FrozenRecordingCases))]
    public async Task Reader_Preserves_Frozen_Recording(
        bool async,
        bool stopRecording,
        bool leaveOpen,
        bool seekable,
        int factory
    )
    {
        using var archive = new MemoryStream();
        using (
            var writer = WriterFactory.OpenWriter(
                archive,
                ArchiveType.Tar,
                new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }
            )
        )
        {
            writer.Write("empty.txt", Stream.Null);
        }
        archive.Position = 0;
        using Stream input = seekable ? archive : new ForwardOnlyStream(archive);
        // Force a recording buffer even when the backing stream supports native seeking.
        using var buffered = new SharpCompressStream(input);
        buffered.StartRecording(TarWrapper.MaximumRewindBufferSize);
        var header = new byte[512];
        Assert.Equal(header.Length, buffered.Read(header, 0, header.Length));
        if (stopRecording)
        {
            buffered.StopRecording();
        }
        else
        {
            buffered.Rewind(true);
        }
        Assert.Equal(0, buffered.Position);
        var options = new ReaderOptions { LeaveStreamOpen = leaveOpen };
        if (async)
        {
            await using var reader = await OpenAsyncReader(buffered, options, factory);
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal("empty.txt", reader.Entry.Key);
            Assert.Equal(0, reader.Entry.Size);
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = OpenReader(buffered, options, factory);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("empty.txt", reader.Entry.Key);
            Assert.Equal(0, reader.Entry.Size);
            Assert.False(reader.MoveToNextEntry());
        }
        Assert.Equal(leaveOpen, input.CanRead);
        Assert.Equal(leaveOpen, buffered.CanRead);
    }

    [Theory]
    [InlineData("invalid", false)]
    [InlineData("invalid", true)]
    [InlineData("throw", false)]
    [InlineData("throw", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    public async Task Failed_LZip_Probe_Releases_Buffer(string failure, bool leaveOpen)
    {
        using var source = new MemoryStream();
        var content = new byte[1024];
        new Random(42).NextBytes(content);
        using (
            var lzip = LZipStream.Create(
                SharpCompressStream.CreateNonDisposing(source),
                SharpCompress.Compressors.CompressionMode.Compress
            )
        )
        {
            lzip.Write(content, 0, content.Length);
        }
        source.Position = 0;
        using var cancellation = new CancellationTokenSource();
        var rings = new List<RingBuffer>();
        var provider = new ProbeLZipProvider(stream =>
        {
            rings.AddRange(GetRings(stream));
            if (failure == "throw")
            {
                throw new IOException("Probe creation failed.");
            }
            if (failure == "cancel")
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
            return LZipStream.Create(stream, SharpCompress.Compressors.CompressionMode.Decompress);
        });
        var options = new ReaderOptions
        {
            LeaveStreamOpen = leaveOpen,
            Providers = CompressionProviderRegistry.Default.With(provider),
        };
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var reader = await TarReader.OpenAsyncReader(
                source,
                options,
                cancellation.Token
            );
        });
        if (failure == "invalid")
        {
            Assert.IsType<InvalidFormatException>(exception);
        }
        else if (failure == "throw")
        {
            Assert.IsType<IOException>(exception);
        }
        else
        {
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
        }
        Assert.NotEmpty(rings);
        var buffer = typeof(RingBuffer).GetField(
            "_buffer",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        Assert.All(rings, ring => Assert.Null(buffer.GetValue(ring)));
        Assert.Equal(leaveOpen, source.CanRead);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Reader_Dispose_Uses_Async_Source_Disposal(int factory, bool leaveOpen)
    {
        await using var input = new AsyncDisposeOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"))
        );
        var reader = await OpenAsyncReader(
            input,
            new ReaderOptions { LeaveStreamOpen = leaveOpen },
            factory
        );
        await reader.DisposeAsync();
        await reader.DisposeAsync();
        Assert.Equal(!leaveOpen, input.AsyncDisposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Failed_Probe_Uses_Async_Source_Disposal(bool cancel, bool leaveOpen)
    {
        await using var input = new AsyncDisposeOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"))
        );
        using var cancellation = new CancellationTokenSource();
        var failure = new IOException("Probe creation failed.");
        var provider = new ProbeLZipProvider(_ =>
        {
            if (cancel)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
            throw failure;
        });
        var options = new ReaderOptions
        {
            LeaveStreamOpen = leaveOpen,
            Providers = CompressionProviderRegistry.Default.With(provider),
        };
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var reader = await TarReader.OpenAsyncReader(
                input,
                options,
                cancellation.Token
            );
        });
        if (cancel)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
        }
        else
        {
            Assert.Same(failure, exception);
        }
        Assert.Equal(!leaveOpen, input.AsyncDisposed);
    }

    [Fact]
    public async Task Canceled_LZip_Header_Buffer_Uses_Async_Source_Disposal()
    {
        await using var input = new AsyncDisposeOnlyStream(new MemoryStream());
        var wrapper = SharpCompressStream.Create(input);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await TarFactory.BufferLZipHeaderAsync(wrapper, new CancellationToken(true))
        );
        Assert.True(input.AsyncDisposed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Probe_Uses_Async_Disposal(int factory)
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"));
        AsyncDisposeOnlyStream? probe = null;
        var provider = new ProbeLZipProvider(stream =>
        {
            probe = new AsyncDisposeOnlyStream(
                LZipStream.Create(stream, SharpCompress.Compressors.CompressionMode.Decompress)
            );
            return probe;
        });
        var options = new ReaderOptions
        {
            LeaveStreamOpen = true,
            Providers = CompressionProviderRegistry.Default.With(provider),
        };
        await using var reader = await OpenAsyncReader(input, options, factory);
        Assert.NotNull(probe);
        Assert.True(probe.AsyncDisposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Probe_Disposal_Failure_Releases_Buffer(bool async, bool leaveOpen)
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"));
        using var input = new ForwardOnlyStream(file);
        var rings = new List<RingBuffer>();
        var probes = new List<SharpCompressStream>();
        var provider = new ProbeLZipProvider(stream =>
        {
            rings.AddRange(GetRings(stream));
            var decompressed = LZipStream.Create(
                stream,
                SharpCompress.Compressors.CompressionMode.Decompress
            );
            var probe = new SharpCompressStream(decompressed) { ThrowOnDispose = true };
            probes.Add(probe);
            return probe;
        });
        var options = new ReaderOptions
        {
            LeaveStreamOpen = leaveOpen,
            Providers = CompressionProviderRegistry.Default.With(provider),
        };
        try
        {
            if (async)
            {
                await Assert.ThrowsAsync<ArchiveOperationException>(async () =>
                {
                    await using var reader = await new TarFactory().OpenAsyncReader(
                        input,
                        options,
                        CancellationToken.None
                    );
                });
            }
            else
            {
                Assert.Throws<ArchiveOperationException>(() =>
                {
                    using var reader = new TarFactory().OpenReader(input, options);
                });
            }
            Assert.NotEmpty(rings);
            var buffer = typeof(RingBuffer).GetField(
                "_buffer",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            Assert.All(rings, ring => Assert.Null(buffer.GetValue(ring)));
            Assert.True(input.CanRead);
        }
        finally
        {
            foreach (var probe in probes)
            {
                probe.ThrowOnDispose = false;
                await probe.DisposeAsync();
            }
        }
    }

    private sealed class AsyncDisposeOnlyStream(Stream stream) : AsyncOnlyStream(stream)
    {
        public bool AsyncDisposed { get; private set; }

        public override ValueTask DisposeAsync()
        {
            AsyncDisposed = true;
            return base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !AsyncDisposed)
            {
                throw new NotSupportedException("Synchronous disposal is not supported.");
            }
            base.Dispose(disposing);
        }
    }

    private sealed class ProbeLZipProvider(Func<Stream, Stream> createStream)
        : CompressionProviderBase
    {
        public override CompressionType CompressionType => CompressionType.LZip;
        public override bool SupportsCompression => false;
        public override bool SupportsDecompression => true;

        public override Stream CreateCompressStream(Stream destination, int compressionLevel) =>
            throw new NotSupportedException();

        public override Stream CreateDecompressStream(Stream source) => createStream(source);
    }

    public static IEnumerable<object[]> ReaderCases()
    {
        foreach (
            var filename in new[]
            {
                "Tar.tar",
                "Tar.tar.gz",
                "Tar.tar.bz2",
                "Tar.tar.zst",
                "Tar.tar.lz",
                "Tar.tar.xz",
                "Tar.tar.Z",
            }
        )
        {
            foreach (var seekable in new[] { false, true })
            {
                foreach (var leaveOpen in new[] { false, true })
                {
                    foreach (var async in new[] { false, true })
                    {
                        foreach (var factory in new[] { 0, 1, 2 })
                        {
                            yield return [filename, seekable, leaveOpen, async, factory];
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ReaderCases))]
    public async Task Reader_Dispose_Releases_Internal_Buffers(
        string filename,
        bool seekable,
        bool leaveOpen,
        bool async,
        int factory
    )
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using Stream input = seekable ? file : new ForwardOnlyStream(file);
        var options = new ReaderOptions { LeaveStreamOpen = leaveOpen };
        var reader = async
            ? (TarReader)await OpenAsyncReader(input, options, factory)
            : (TarReader)OpenReader(input, options, factory);
        var rings = GetRings(reader.Volume.Stream);
        if (seekable && !(async && filename == "Tar.tar.lz"))
        {
            Assert.Empty(rings);
        }
        else if (!seekable)
        {
            Assert.Contains(rings, ring => ring.Capacity >= TarWrapper.MaximumRewindBufferSize);
        }
        else
        {
            Assert.NotEmpty(rings);
        }

        var entries = 0;
        try
        {
            while (
                async
                    ? await reader.MoveToNextEntryAsync(CancellationToken.None)
                    : reader.MoveToNextEntry()
            )
            {
                if (!reader.Entry.IsDirectory)
                {
                    if (async)
                    {
                        await reader.WriteEntryToAsync(Stream.Null, CancellationToken.None);
                    }
                    else
                    {
                        reader.WriteEntryTo(Stream.Null);
                    }
                    entries++;
                }
            }
        }
        finally
        {
            if (async)
            {
                await reader.DisposeAsync();
                await reader.DisposeAsync();
            }
            reader.Dispose();
        }

        Assert.True(entries > 0);
        var buffer = typeof(RingBuffer).GetField(
            "_buffer",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        Assert.All(rings, ring => Assert.Null(buffer.GetValue(ring)));
        Assert.Equal(leaveOpen, input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BZip2_Probe_Preserves_Large_NonSeekable_Block(bool async)
    {
        var content = new byte[850_000];
        new Random(42).NextBytes(content);
        using var archive = new MemoryStream();
        using (
            var writer = WriterFactory.OpenWriter(
                archive,
                ArchiveType.Tar,
                new WriterOptions(CompressionType.BZip2) { LeaveStreamOpen = true }
            )
        )
        using (var source = new MemoryStream(content))
        {
            writer.Write("random.bin", source);
        }
        Assert.True(archive.Length > 81920);
        archive.Position = 0;
        using var input = new ForwardOnlyStream(archive);
        if (async)
        {
            await using var reader = await ReaderFactory.OpenAsyncReader(
                input,
                cancellationToken: CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            using var output = new MemoryStream();
            await reader.WriteEntryToAsync(output, CancellationToken.None);
            Assert.Equal(content, output.ToArray());
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(input);
            Assert.True(reader.MoveToNextEntry());
            using var output = new MemoryStream();
            reader.WriteEntryTo(output);
            Assert.Equal(content, output.ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_Tar_Probe_Does_Not_Close_Source(bool async)
    {
        using var source = new MemoryStream();
        var content = new byte[1024];
        new Random(42).NextBytes(content);
        using (
            var gzip = new GZipStream(
                SharpCompressStream.CreateNonDisposing(source),
                SharpCompress.Compressors.CompressionMode.Compress
            )
        )
        {
            gzip.Write(content, 0, content.Length);
        }
        source.Position = 0;
        using var input = new ForwardOnlyStream(source);
        var factory = new TarFactory();
        if (async)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await factory.OpenAsyncReader(
                    input,
                    ReaderOptions.ForExternalStream,
                    CancellationToken.None
                )
            );
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() =>
                factory.OpenReader(input, ReaderOptions.ForExternalStream)
            );
        }
        Assert.True(input.CanRead);
    }

    private static IReader OpenReader(Stream input, ReaderOptions options, int factory) =>
        factory switch
        {
            0 => ReaderFactory.OpenReader(input, options),
            1 => new TarFactory().OpenReader(input, options),
            _ => TarReader.OpenReader(input, options),
        };

    private static ValueTask<IAsyncReader> OpenAsyncReader(
        Stream input,
        ReaderOptions options,
        int factory
    ) =>
        factory switch
        {
            0 => ReaderFactory.OpenAsyncReader(input, options, CancellationToken.None),
            1 => new TarFactory().OpenAsyncReader(input, options, CancellationToken.None),
            _ => TarReader.OpenAsyncReader(input, options, CancellationToken.None),
        };

    private static List<RingBuffer> GetRings(Stream stream)
    {
        var rings = new List<RingBuffer>();
        var field = typeof(SharpCompressStream).GetField(
            "_ringBuffer",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        while (stream is SharpCompressStream wrapper)
        {
            if (field.GetValue(wrapper) is RingBuffer ring)
            {
                rings.Add(ring);
            }
            stream = wrapper.BaseStream();
        }
        return rings;
    }
}
