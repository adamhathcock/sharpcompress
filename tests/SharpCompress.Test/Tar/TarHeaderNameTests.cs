using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarHeaderNameTests
{
    private static readonly DateTime Modified = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Name fields end at the first NUL. Leading and trailing spaces are part of
    // the name and must survive a round trip, including GNU long names.
    private static readonly string[] NamesWithSpaces =
    [
        " lead.txt",
        "trail.txt ",
        " both ",
        "dir /file.txt",
        "dir/" + new string('n', 120) + " ",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Names_Keep_Leading_And_Trailing_Spaces(bool useAsync)
    {
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true);
        using (var writer = new TarWriter(saved, options))
        {
            foreach (var name in NamesWithSpaces)
            {
                writer.Write(name, new MemoryStream(Encoding.ASCII.GetBytes("x")), Modified);
            }
        }

        saved.Position = 0;
        Assert.Equal(NamesWithSpaces, await ReadKeys(saved, useAsync));

        saved.Position = 0;
        using var archive = TarArchive.OpenArchive(saved, ReaderOptions.ForExternalStream);
        var keys = new List<string?>();
        foreach (var entry in archive.Entries)
        {
            keys.Add(entry.Key);
        }
        Assert.Equal(NamesWithSpaces, keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Names_Round_Trip_With_Utf16_Encoding(bool useAsync)
    {
        // UTF-16LE encodes ASCII characters with a zero byte, so the name must
        // be decoded before looking for the NUL terminator.
        string[] names = ["notes.txt", " lead.txt", "dir/trail.txt "];
        var encoding = new ArchiveEncoding { Default = Encoding.Unicode };

        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true)
        {
            ArchiveEncoding = encoding,
        };
        using (var writer = new TarWriter(saved, options))
        {
            foreach (var name in names)
            {
                writer.Write(name, new MemoryStream(Encoding.ASCII.GetBytes("x")), Modified);
            }
        }

        saved.Position = 0;
        Assert.Equal(
            names,
            await ReadKeys(
                saved,
                useAsync,
                ReaderOptions.ForExternalStream.WithArchiveEncoding(encoding)
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ustar_Prefix_And_Link_Target_Keep_Spaces(bool useAsync)
    {
        using var saved = new MemoryStream();
        var file = UstarHeader("file.txt", '0', linkName: "", prefix: "pre fix ");
        var link = UstarHeader("link", '2', linkName: " target ", prefix: "");
        saved.Write(file, 0, file.Length);
        saved.Write(link, 0, link.Length);
        saved.Write(new byte[1024], 0, 1024);

        saved.Position = 0;
        if (useAsync)
        {
            await using var reader = await TarReader.OpenAsyncReader(
                new AsyncOnlyStream(saved, false),
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal("pre fix /file.txt", reader.Entry.Key);
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(" target ", reader.Entry.LinkTarget);
        }
        else
        {
            using var reader = TarReader.OpenReader(saved, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("pre fix /file.txt", reader.Entry.Key);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(" target ", reader.Entry.LinkTarget);
        }
    }

    private static async Task<List<string?>> ReadKeys(
        Stream stream,
        bool useAsync,
        ReaderOptions? options = null
    )
    {
        options ??= ReaderOptions.ForExternalStream;
        var keys = new List<string?>();
        if (useAsync)
        {
            await using var reader = await TarReader.OpenAsyncReader(
                new AsyncOnlyStream(stream, false),
                options,
                CancellationToken.None
            );
            while (await reader.MoveToNextEntryAsync(CancellationToken.None))
            {
                keys.Add(reader.Entry.Key);
            }
        }
        else
        {
            using var reader = TarReader.OpenReader(stream, options);
            while (reader.MoveToNextEntry())
            {
                keys.Add(reader.Entry.Key);
            }
        }
        return keys;
    }

    private static byte[] UstarHeader(string name, char type, string linkName, string prefix)
    {
        var buffer = new byte[512];
        void Put(string value, int offset) => Encoding.ASCII.GetBytes(value).CopyTo(buffer, offset);

        Put(name, 0);
        Put("0000644\0", 100);
        Put("0000000\0", 108);
        Put("0000000\0", 116);
        Put("00000000000\0", 124);
        Put("13600000000\0", 136);
        Put("        ", 148);
        buffer[156] = (byte)type;
        Put(linkName, 157);
        Put("ustar\0", 257);
        Put("00", 263);
        Put(prefix, 345);

        var checksum = 0;
        foreach (var b in buffer)
        {
            checksum += b;
        }
        Put(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ", 148);
        return buffer;
    }
}
