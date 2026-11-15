using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipDirectoryPayloadTests
{
    public enum Descriptor
    {
        None,
        Signed,
        Unsigned,
        KnownSizes,
    }

    public enum DirectoryOperation
    {
        Skip,
        Read,
        Write,
        DisposeUnread,
    }

    public static IEnumerable<object[]> ReaderCases()
    {
        foreach (var deflated in new[] { false, true })
        {
            foreach (
                var descriptor in new[]
                {
                    Descriptor.None,
                    Descriptor.Signed,
                    Descriptor.Unsigned,
                    Descriptor.KnownSizes,
                }
            )
            {
                foreach (var nonSeekable in new[] { false, true })
                {
                    foreach (var factory in new[] { false, true })
                    {
                        foreach (
                            var operation in new[]
                            {
                                DirectoryOperation.Skip,
                                DirectoryOperation.Read,
                                DirectoryOperation.Write,
                                DirectoryOperation.DisposeUnread,
                            }
                        )
                        {
                            yield return new object[]
                            {
                                deflated,
                                descriptor,
                                nonSeekable,
                                factory,
                                operation,
                            };
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ReaderCases))]
    public void Reader_Continues_After_Directory(
        bool deflated,
        Descriptor descriptor,
        bool nonSeekable,
        bool factory,
        DirectoryOperation operation
    )
    {
        using var stream = OpenStream(CreateArchive(deflated, descriptor), nonSeekable);
        using (
            var reader = factory
                ? ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream)
                : ZipReader.OpenReader(stream, ReaderOptions.ForExternalStream)
        )
        {
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("first.txt", reader.Entry.Key);
            // Leave the first file unread to exercise automatic skipping too.
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("dir/", reader.Entry.Key);
            Assert.True(reader.Entry.IsDirectory);
            Assert.Equal(0, reader.Entry.Size);

            switch (operation)
            {
                case DirectoryOperation.Read:
                    using (var entryStream = reader.OpenEntryStream())
                    {
                        Assert.Equal(-1, entryStream.ReadByte());
                    }
                    break;
                case DirectoryOperation.Write:
                    using (var output = new MemoryStream())
                    {
                        reader.WriteEntryTo(output);
                        Assert.Equal(0, output.Length);
                    }
                    break;
                case DirectoryOperation.DisposeUnread:
                    reader.OpenEntryStream().Dispose();
                    break;
                // Otherwise leave the directory unopened for automatic skipping.
            }

            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("dir/file.txt", reader.Entry.Key);
            using var contents = new MemoryStream();
            reader.WriteEntryTo(contents);
            Assert.Equal("hello", Encoding.UTF8.GetString(contents.ToArray()));
            Assert.False(reader.MoveToNextEntry());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [MemberData(nameof(ReaderCases))]
    public async Task Reader_Continues_After_Directory_Async(
        bool deflated,
        Descriptor descriptor,
        bool nonSeekable,
        bool factory,
        DirectoryOperation operation
    )
    {
        var cancellationToken = CancellationToken.None;
        using var stream = new AsyncOnlyStream(
            OpenStream(CreateArchive(deflated, descriptor), nonSeekable)
        );
        await using (
            var reader = factory
                ? await ReaderFactory.OpenAsyncReader(
                    stream,
                    ReaderOptions.ForExternalStream,
                    cancellationToken
                )
                : (IAsyncReader)ZipReader.OpenReader(stream, ReaderOptions.ForExternalStream)
        )
        {
            Assert.True(await reader.MoveToNextEntryAsync(cancellationToken));
            Assert.Equal("first.txt", reader.Entry.Key);
            Assert.True(await reader.MoveToNextEntryAsync(cancellationToken));
            Assert.Equal("dir/", reader.Entry.Key);
            Assert.True(reader.Entry.IsDirectory);
            Assert.Equal(0, reader.Entry.Size);

            switch (operation)
            {
                case DirectoryOperation.Read:
                    await using (
                        var entryStream = await reader.OpenEntryStreamAsync(cancellationToken)
                    )
                    {
                        Assert.Equal(
                            0,
                            await entryStream.ReadAsync(new byte[1], 0, 1, cancellationToken)
                        );
                    }
                    break;
                case DirectoryOperation.Write:
                    using (var output = new MemoryStream())
                    {
                        await reader.WriteEntryToAsync(output, cancellationToken);
                        Assert.Equal(0, output.Length);
                    }
                    break;
                case DirectoryOperation.DisposeUnread:
                    var unreadStream = await reader.OpenEntryStreamAsync(cancellationToken);
                    await unreadStream.DisposeAsync();
                    break;
            }

            Assert.True(await reader.MoveToNextEntryAsync(cancellationToken));
            Assert.Equal("dir/file.txt", reader.Entry.Key);
            using var contents = new MemoryStream();
            await reader.WriteEntryToAsync(contents, cancellationToken);
            Assert.Equal("hello", Encoding.UTF8.GetString(contents.ToArray()));
            Assert.False(await reader.MoveToNextEntryAsync(cancellationToken));
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(Descriptor.None)]
    [InlineData(Descriptor.Signed)]
    [InlineData(Descriptor.Unsigned)]
    [InlineData(Descriptor.KnownSizes)]
    public void Archive_Reads_Deflated_Directory(Descriptor descriptor)
    {
        using var stream = new MemoryStream(CreateArchive(deflated: true, descriptor));
        using var archive = ZipArchive.OpenArchive(stream, ReaderOptions.ForExternalStream);
        var entries = archive.Entries.ToArray();
        Assert.Equal(new[] { "first.txt", "dir/", "dir/file.txt" }, entries.Select(e => e.Key));
        Assert.True(entries[1].IsDirectory);
        using var entryStream = entries[2].OpenEntryStream();
        using var contents = new MemoryStream();
        entryStream.CopyTo(contents);
        Assert.Equal("hello", Encoding.UTF8.GetString(contents.ToArray()));
    }

    private static Stream OpenStream(byte[] archive, bool nonSeekable)
    {
        var stream = new MemoryStream(archive);
        return nonSeekable ? new ForwardOnlyStream(stream) : stream;
    }

    private static byte[] CreateArchive(bool deflated, Descriptor descriptor)
    {
        // Build the records explicitly: writers often normalize empty entries to Stored,
        // hiding the nonzero compressed payload of an empty Deflate directory (issue #1493).
        var entries = new[]
        {
            (Name: "first.txt", Data: Encoding.UTF8.GetBytes("hello"), Size: 5u, Crc: 0x3610A686u),
            (Name: "dir/", Data: deflated ? new byte[] { 0x03, 0x00 } : [], Size: 0u, Crc: 0u),
            (
                Name: "dir/file.txt",
                Data: Encoding.UTF8.GetBytes("hello"),
                Size: 5u,
                Crc: 0x3610A686u
            ),
        };
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var offsets = new List<uint>();
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var name = Encoding.UTF8.GetBytes(entry.Name);
            var hasDescriptor = i == 1 && descriptor != Descriptor.None;
            // KnownSizes uses a signed descriptor but also supplies the local sizes.
            var unknownSizes = hasDescriptor && descriptor != Descriptor.KnownSizes;
            offsets.Add((uint)stream.Position);
            writer.Write(0x04034B50u);
            writer.Write((ushort)20);
            writer.Write((ushort)(hasDescriptor ? 8 : 0));
            writer.Write((ushort)(i == 1 && deflated ? 8 : 0));
            writer.Write(0u); // DOS time/date
            writer.Write(unknownSizes ? 0u : entry.Crc);
            writer.Write(unknownSizes ? 0u : (uint)entry.Data.Length);
            writer.Write(unknownSizes ? 0u : entry.Size);
            writer.Write((ushort)name.Length);
            writer.Write((ushort)0); // extra length
            writer.Write(name);
            writer.Write(entry.Data);
            if (hasDescriptor)
            {
                if (descriptor != Descriptor.Unsigned)
                {
                    writer.Write(0x08074B50u);
                }
                writer.Write(entry.Crc);
                writer.Write((uint)entry.Data.Length);
                writer.Write(entry.Size);
            }
        }

        var centralDirectoryOffset = (uint)stream.Position;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var name = Encoding.UTF8.GetBytes(entry.Name);
            writer.Write(0x02014B50u);
            writer.Write((ushort)20); // version made by
            writer.Write((ushort)20);
            writer.Write((ushort)(i == 1 && descriptor != Descriptor.None ? 8 : 0));
            writer.Write((ushort)(i == 1 && deflated ? 8 : 0));
            writer.Write(0u); // DOS time/date
            writer.Write(entry.Crc);
            writer.Write((uint)entry.Data.Length);
            writer.Write(entry.Size);
            writer.Write((ushort)name.Length);
            writer.Write((ushort)0); // extra length
            writer.Write((ushort)0); // comment length
            writer.Write((ushort)0); // disk number
            writer.Write((ushort)0); // internal attributes
            writer.Write(i == 1 ? 0x10u : 0u); // external attributes
            writer.Write(offsets[i]);
            writer.Write(name);
        }

        var centralDirectorySize = (uint)stream.Position - centralDirectoryOffset;
        writer.Write(0x06054B50u);
        writer.Write((ushort)0); // disk number
        writer.Write((ushort)0); // central directory disk
        writer.Write((ushort)entries.Length);
        writer.Write((ushort)entries.Length);
        writer.Write(centralDirectorySize);
        writer.Write(centralDirectoryOffset);
        writer.Write((ushort)0); // comment length
        return stream.ToArray();
    }
}
