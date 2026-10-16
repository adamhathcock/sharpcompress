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
