using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class WritableArchiveIdentityTests
{
    [Theory]
    [InlineData(ArchiveType.Zip, false, false)]
    [InlineData(ArchiveType.Zip, false, true)]
    [InlineData(ArchiveType.Zip, true, false)]
    [InlineData(ArchiveType.Zip, true, true)]
    [InlineData(ArchiveType.Tar, false, false)]
    [InlineData(ArchiveType.Tar, false, true)]
    [InlineData(ArchiveType.Tar, true, false)]
    [InlineData(ArchiveType.Tar, true, true)]
    public async Task Removal_Survives_Switching_Apis_And_Rebuilding(
        ArchiveType type,
        bool asyncFirst,
        bool paused
    )
    {
        using var input = CreateArchive(type, "remove", "keep");
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        var writable = (IWritableArchive)archive;
        var asyncWritable = (IWritableAsyncArchive)archive;
        // Leave the first loader partially consumed before switching APIs.
        var entry = asyncFirst
            ? await asyncWritable.EntriesAsync.FirstAsync()
            : archive.Entries.First();
        var otherEntry = asyncFirst
            ? archive.Entries.First()
            : await asyncWritable.EntriesAsync.FirstAsync();
        Assert.Same(entry, otherEntry);

        using var source = new MemoryStream(new byte[] { 42 });
        using (paused ? writable.PauseEntryRebuilding() : null)
        {
            if (asyncFirst)
            {
                writable.RemoveEntry(entry);
                writable.AddEntry("new", source, false);
            }
            else
            {
                await asyncWritable.RemoveEntryAsync(entry);
                await asyncWritable.AddEntryAsync(
                    "new",
                    source,
                    false,
                    cancellationToken: CancellationToken.None
                );
            }
        }
        var syncEntries = archive.Entries.ToList();
        var asyncEntries = await asyncWritable.EntriesAsync.ToListAsync();
        Assert.Equal(new[] { "keep", "new" }, syncEntries.Select(e => e.Key));
        Assert.Equal(syncEntries, asyncEntries);

        foreach (var useAsync in new[] { false, true })
        {
            using var output = new MemoryStream();
            await Save(archive, output, useAsync);
            output.Position = 0;
            using var saved = ArchiveFactory.OpenArchive(output, ReaderOptions.ForExternalStream);
            Assert.Equal(new[] { "keep", "new" }, saved.Entries.Select(e => e.Key));
        }
        Assert.True(input.CanRead);
        Assert.True(source.CanRead);
    }

    [Theory]
    [InlineData(ArchiveType.Zip, false)]
    [InlineData(ArchiveType.Zip, true)]
    [InlineData(ArchiveType.Tar, false)]
    [InlineData(ArchiveType.Tar, true)]
    public async Task Shared_Identity_Does_Not_Remove_Other_Entries_With_The_Same_Name(
        ArchiveType type,
        bool asyncFirst
    )
    {
        using var input = CreateArchive(type, "same", "same");
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IWritableAsyncArchive)archive;
        if (asyncFirst)
        {
            var entry = await asyncArchive.EntriesAsync.FirstAsync();
            ((IWritableArchive)archive).RemoveEntry(entry);
        }
        else
        {
            await asyncArchive.RemoveEntryAsync(archive.Entries.First());
        }
        var remaining = Assert.Single(archive.Entries);
        Assert.Same(remaining, Assert.Single(await asyncArchive.EntriesAsync.ToListAsync()));
        using var payload = remaining.OpenEntryStream();
        Assert.Equal(2, payload.ReadByte());
    }

    [Theory]
    [InlineData(ArchiveType.Zip)]
    [InlineData(ArchiveType.Tar)]
    public async Task Partially_Consumed_Loaders_Resume_From_The_Shared_Cache(ArchiveType type)
    {
        using var input = CreateArchive(type, "first", "second", "third");
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        using var syncEntries = archive.Entries.GetEnumerator();
        await using var asyncEntries = ((IAsyncArchive)archive).EntriesAsync.GetAsyncEnumerator(
            CancellationToken.None
        );
        Assert.True(syncEntries.MoveNext());
        Assert.True(await asyncEntries.MoveNextAsync());
        Assert.Same(syncEntries.Current, asyncEntries.Current);
        Assert.True(await asyncEntries.MoveNextAsync());
        var second = asyncEntries.Current;
        Assert.True(syncEntries.MoveNext());
        Assert.Same(second, syncEntries.Current);
        Assert.True(syncEntries.MoveNext());
        Assert.True(await asyncEntries.MoveNextAsync());
        Assert.Same(syncEntries.Current, asyncEntries.Current);
        Assert.False(syncEntries.MoveNext());
        Assert.False(await asyncEntries.MoveNextAsync());
    }

    [Theory]
    [InlineData(ArchiveType.Zip)]
    [InlineData(ArchiveType.Tar)]
    public async Task Alternating_Loaders_Read_Only_A_Linear_Number_Of_Headers(ArchiveType type)
    {
        const int count = 128;
        var names = Enumerable.Range(0, count).Select(i => $"entry-{i}").ToArray();
        using var data = CreateArchive(type, names);
        using var input = new CountingReadStream(data);
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        using var syncEntries = archive.Entries.GetEnumerator();
        await using var asyncEntries = ((IAsyncArchive)archive).EntriesAsync.GetAsyncEnumerator(
            CancellationToken.None
        );
        for (var i = 0; i < count; i++)
        {
            if ((i & 1) == 0)
            {
                Assert.True(syncEntries.MoveNext());
                Assert.True(await asyncEntries.MoveNextAsync());
            }
            else
            {
                Assert.True(await asyncEntries.MoveNextAsync());
                Assert.True(syncEntries.MoveNext());
            }
            Assert.Same(syncEntries.Current, asyncEntries.Current);
            Assert.Equal(names[i], syncEntries.Current.Key);
        }
        Assert.False(syncEntries.MoveNext());
        Assert.False(await asyncEntries.MoveNextAsync());
        // Allow two scans and ZIP's end-of-directory search, but not a prefix
        // reparse for every entry. This bound does not depend on timing.
        Assert.True(
            input.BytesRead <= input.Length * 6,
            $"Read {input.BytesRead} bytes for a {input.Length}-byte archive."
        );
    }

    [Theory]
    [InlineData(ArchiveType.Zip, false)]
    [InlineData(ArchiveType.Zip, true)]
    [InlineData(ArchiveType.Tar, false)]
    [InlineData(ArchiveType.Tar, true)]
    public async Task Partial_Enumerations_Stay_Lazy_And_Reuse_Their_Loader(
        ArchiveType type,
        bool useAsync
    )
    {
        var names = Enumerable.Range(0, 16).Select(i => $"entry-{i}").ToArray();
        using var data = CreateArchive(type, names);
        using var input = new CountingReadStream(data);
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IAsyncArchive)archive;
        var first = useAsync
            ? await asyncArchive.EntriesAsync.FirstAsync()
            : archive.Entries.First();
        var readsAfterFirst = input.BytesRead;
        var repeated = useAsync
            ? await asyncArchive.EntriesAsync.FirstAsync()
            : archive.Entries.First();
        Assert.Same(first, repeated);
        Assert.Equal(readsAfterFirst, input.BytesRead);
        var entries = useAsync
            ? await asyncArchive.EntriesAsync.ToListAsync()
            : archive.Entries.ToList();
        Assert.Equal(names, entries.Select(e => e.Key));
        Assert.True(
            input.BytesRead > readsAfterFirst,
            "The first request must not load every header."
        );
    }

    [Theory]
    [InlineData(ArchiveType.Zip)]
    [InlineData(ArchiveType.Tar)]
    public async Task Cancellation_During_A_Read_Does_Not_Lose_An_Entry(ArchiveType type)
    {
        using var data = CreateArchive(type, "first", "second", "third");
        using var input = new CountingReadStream(data);
        using var cancellation = new CancellationTokenSource();
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            input,
            ReaderOptions.ForExternalStream,
            CancellationToken.None
        );
        var first = await archive.EntriesAsync.FirstAsync();
        await using (var entries = archive.EntriesAsync.GetAsyncEnumerator(cancellation.Token))
        {
            Assert.True(await entries.MoveNextAsync());
            Assert.Same(first, entries.Current);
            input.AfterRead = cancellation.Cancel;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await entries.MoveNextAsync()
            );
        }
        input.AfterRead = null;
        var remaining = await archive.EntriesAsync.ToListAsync();
        Assert.Equal(new[] { "first", "second", "third" }, remaining.Select(e => e.Key));
        Assert.Same(first, remaining[0]);
    }

    private sealed class CountingReadStream(Stream source) : Stream
    {
        public long BytesRead { get; private set; }
        public Action? AfterRead { get; set; }
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position
        {
            get => source.Position;
            set => source.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = source.Read(buffer, offset, count);
            BytesRead += read;
            AfterRead?.Invoke();
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            var read = await source.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += read;
            AfterRead?.Invoke();
            return read;
        }

#if !LEGACY_DOTNET
        public override int Read(Span<byte> buffer)
        {
            var read = source.Read(buffer);
            BytesRead += read;
            AfterRead?.Invoke();
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            AfterRead?.Invoke();
            return read;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);

        public override void Flush() => source.Flush();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private static MemoryStream CreateArchive(ArchiveType type, params string[] names)
    {
        var input = new MemoryStream();
        using (
            var writer = WriterFactory.OpenWriter(
                input,
                type,
                new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }
            )
        )
        {
            for (var i = 0; i < names.Length; i++)
            {
                using var source = new MemoryStream(new[] { (byte)(i + 1) });
                writer.Write(names[i], source, DateTime.UtcNow);
            }
        }
        input.Position = 0;
        return input;
    }

    private static async Task Save(IArchive archive, Stream output, bool useAsync)
    {
        if (archive is ZipArchive zip)
        {
            var options = new ZipWriterOptions(CompressionType.None) { LeaveStreamOpen = true };
            if (useAsync)
            {
                await zip.SaveToAsync(output, options, CancellationToken.None);
            }
            else
            {
                zip.SaveTo(output, options);
            }
        }
        else
        {
            var tar = (TarArchive)archive;
            var options = new TarWriterOptions(CompressionType.None) { LeaveStreamOpen = true };
            if (useAsync)
            {
                await tar.SaveToAsync(output, options, CancellationToken.None);
            }
            else
            {
                tar.SaveTo(output, options);
            }
        }
    }
}
