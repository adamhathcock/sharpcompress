using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarReaderAsyncTests : ReaderTests
{
    public TarReaderAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask Tar_Reader_Async() => await ReadAsync("Tar.tar", CompressionType.None);

    [Fact]
    public async ValueTask Tar_Skip_Async()
    {
        using Stream stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));
        var x = 0;
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                x++;
                if (x % 2 == 0)
                {
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
                }
            }
        }
    }

    [Fact]
    public async ValueTask Tar_Z_Reader_Async() =>
        await ReadAsync("Tar.tar.Z", CompressionType.Lzw);

    [Fact]
    public async ValueTask Tar_Async_Assert() => await AssertArchiveAsync("Tar.tar");

    [Fact]
    public async ValueTask Tar_BZip2_Reader_Async() =>
        await ReadAsync("Tar.tar.bz2", CompressionType.BZip2);

    [Fact]
    public async ValueTask Tar_GZip_Reader_Async() =>
        await ReadAsync("Tar.tar.gz", CompressionType.GZip);

    [Fact]
    public async ValueTask Tar_ZStandard_Reader_Async() =>
        await ReadAsync("Tar.tar.zst", CompressionType.ZStandard);

    [Fact]
    public async ValueTask Tar_LZip_Reader_Async() =>
        await ReadAsync("Tar.tar.lz", CompressionType.LZip);

    [Fact]
    public async ValueTask Tar_Xz_Reader_Async() =>
        await ReadAsync("Tar.tar.xz", CompressionType.Xz);

    [Fact]
    public async ValueTask Tar_GZip_OldGnu_Reader_Async() =>
        await ReadAsync("Tar.oldgnu.tar.gz", CompressionType.GZip);

    [Fact]
    public async ValueTask Tar_BZip2_Entry_Stream_Async()
    {
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2"));
        await using var reader = await TarReader.OpenAsyncReader(stream);
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                using var entryStream = await reader.OpenEntryStreamAsync();
                var file = Path.GetFileName(reader.Entry.Key);
                var folder =
                    Path.GetDirectoryName(reader.Entry.Key) ?? throw new ArgumentNullException();
                var destdir = Path.Combine(SCRATCH_FILES_PATH, folder);
                if (!Directory.Exists(destdir))
                {
                    Directory.CreateDirectory(destdir);
                }
                var destinationFileName = Path.Combine(destdir, file.NotNull());

                using var fs = File.OpenWrite(destinationFileName);
                await entryStream.CopyToAsync(fs);
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Tar_LongNamesWithLongNameExtension_Async()
    {
        var filePaths = new List<string>();

        using (
            Stream stream = File.OpenRead(
                Path.Combine(TEST_ARCHIVES_PATH, "Tar.LongPathsWithLongNameExtension.tar")
            )
        )
        using (var reader = TarReader.OpenReader(stream))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    filePaths.Add(reader.Entry.Key.NotNull("Entry Key is null"));
                }
            }
        }

        Assert.Equal(3, filePaths.Count);
        Assert.Contains("a.txt", filePaths);
        Assert.Contains(
            "wp-content/plugins/gravityformsextend/lib/Aws/Symfony/Component/ClassLoader/Tests/Fixtures/Apc/beta/Apc/ApcPrefixCollision/A/B/Bar.php",
            filePaths
        );
        Assert.Contains(
            "wp-content/plugins/gravityformsextend/lib/Aws/Symfony/Component/ClassLoader/Tests/Fixtures/Apc/beta/Apc/ApcPrefixCollision/A/B/Foo.php",
            filePaths
        );
    }

    [Fact]
    public async ValueTask Tar_BZip2_Skip_Entry_Stream_Async()
    {
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2"));
        await using var reader = await TarReader.OpenAsyncReader(stream);
        var names = new List<string>();
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                using var entryStream = await reader.OpenEntryStreamAsync();
                await entryStream.SkipEntryAsync();
                names.Add(reader.Entry.Key.NotNull());
            }
        }
        Assert.Equal(3, names.Count);
    }

    [Fact]
    public async ValueTask Tar_PaxLocalHeader_Reader_Async()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxLocalHeader.tar");

        using Stream stream = File.OpenRead(archivePath);
        await using var reader = await TarReader.OpenAsyncReader(stream);

        Assert.True(await reader.MoveToNextEntryAsync());
        var firstEntry = (TarEntry)reader.Entry;
        Assert.Equal("pax/overridden-name.txt", firstEntry.Key);
        Assert.Equal(10, firstEntry.Size);
        Assert.Equal(1234, firstEntry.UserID);
        Assert.Equal(2345, firstEntry.GroupId);
        Assert.Equal(Convert.ToInt64("640", 8), firstEntry.Mode);

        var expectedTime = DateTimeOffset.FromUnixTimeSeconds(1700000000).LocalDateTime;
        Assert.Equal(expectedTime, firstEntry.LastModifiedTime);

        using (var entryStream = await reader.OpenEntryStreamAsync())
        using (var memoryStream = new MemoryStream())
        {
            await entryStream.CopyToAsync(memoryStream);
            Assert.Equal(10, memoryStream.Length);
        }

        Assert.True(await reader.MoveToNextEntryAsync());
        var secondEntry = (TarEntry)reader.Entry;
        Assert.Equal("second.txt", secondEntry.Key);
        Assert.Equal(11, secondEntry.UserID);
        Assert.Equal(22, secondEntry.GroupId);
        Assert.Equal(Convert.ToInt64("644", 8), secondEntry.Mode);
        Assert.Equal(2, secondEntry.Size);

        Assert.False(await reader.MoveToNextEntryAsync());
    }

    [Fact]
    public async ValueTask Tar_PaxLocalHeader_Link_Reader_Async()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxLocalHeader.Link.tar");

        using Stream stream = File.OpenRead(archivePath);
        await using var reader = await TarReader.OpenAsyncReader(stream);

        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.Equal("pax/link-entry", reader.Entry.Key);
        Assert.Equal("pax/target-entry", reader.Entry.LinkTarget);
        Assert.False(reader.Entry.IsDirectory);
        Assert.False(await reader.MoveToNextEntryAsync());
    }

    [Fact]
    public async ValueTask Tar_PaxGlobalHeader_Reader_Async()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxGlobalHeader.tar");

        using Stream stream = File.OpenRead(archivePath);
        await using var reader = await TarReader.OpenAsyncReader(stream);

        var globalTime = DateTimeOffset.FromUnixTimeSeconds(1700000100).LocalDateTime;
        var localOverrideTime = DateTimeOffset.FromUnixTimeSeconds(1700000200).LocalDateTime;

        Assert.True(await reader.MoveToNextEntryAsync());
        var firstEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-one.txt", firstEntry.Key);
        Assert.Equal(4000, firstEntry.UserID);
        Assert.Equal(5000, firstEntry.GroupId);
        Assert.Equal(Convert.ToInt64("640", 8), firstEntry.Mode);
        Assert.Equal(globalTime, firstEntry.LastModifiedTime);

        Assert.True(await reader.MoveToNextEntryAsync());
        var secondEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-local-override.txt", secondEntry.Key);
        Assert.Equal(4010, secondEntry.UserID);
        Assert.Equal(5010, secondEntry.GroupId);
        Assert.Equal(Convert.ToInt64("600", 8), secondEntry.Mode);
        Assert.Equal(localOverrideTime, secondEntry.LastModifiedTime);

        Assert.True(await reader.MoveToNextEntryAsync());
        var thirdEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-three.txt", thirdEntry.Key);
        Assert.Equal(4000, thirdEntry.UserID);
        Assert.Equal(5000, thirdEntry.GroupId);
        Assert.Equal(Convert.ToInt64("640", 8), thirdEntry.Mode);
        Assert.Equal(globalTime, thirdEntry.LastModifiedTime);

        Assert.False(await reader.MoveToNextEntryAsync());
    }

    [Fact]
    public async ValueTask Tar_PaxGlobalHeader_Link_Reader_Async()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxGlobalHeader.Link.tar");

        using Stream stream = File.OpenRead(archivePath);
        await using var reader = await TarReader.OpenAsyncReader(stream);

        Assert.True(await reader.MoveToNextEntryAsync());
        var firstEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-link", firstEntry.Key);
        Assert.Equal("global-target", firstEntry.LinkTarget);
        Assert.Equal(4100, firstEntry.UserID);
        Assert.Equal(5100, firstEntry.GroupId);
        Assert.Equal(Convert.ToInt64("777", 8), firstEntry.Mode);

        Assert.True(await reader.MoveToNextEntryAsync());
        var secondEntry = (TarEntry)reader.Entry;
        Assert.Equal("local-link-override", secondEntry.Key);
        Assert.Equal("local-target", secondEntry.LinkTarget);
        Assert.Equal(4100, secondEntry.UserID);
        Assert.Equal(5100, secondEntry.GroupId);
        Assert.Equal(Convert.ToInt64("777", 8), secondEntry.Mode);

        Assert.False(await reader.MoveToNextEntryAsync());
    }

    [Fact]
    public async ValueTask Tar_WithSymlink_Reader_SurfacesLinkTargets_Async()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "TarWithSymlink.tar.gz");

        using Stream stream = File.OpenRead(archivePath);
        await using var reader = await TarReader.OpenAsyncReader(stream);

        var foundVulkanToolsLink = false;
        var foundVulkanSamplesLink = false;

        while (await reader.MoveToNextEntryAsync())
        {
            if (reader.Entry.Key == "MoltenVK-1.0.21/Demos/LunarG-VulkanSamples/Vulkan-Tools")
            {
                foundVulkanToolsLink = true;
                Assert.Equal("../../External/Vulkan-Tools", reader.Entry.LinkTarget);
            }

            if (reader.Entry.Key == "MoltenVK-1.0.21/Demos/LunarG-VulkanSamples/VulkanSamples")
            {
                foundVulkanSamplesLink = true;
                Assert.Equal("../../External/VulkanSamples", reader.Entry.LinkTarget);
            }
        }

        Assert.True(foundVulkanToolsLink);
        Assert.True(foundVulkanSamplesLink);
    }

    [Fact]
    public void Tar_Containing_Rar_Reader_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.OpenReader(stream);
        Assert.True(reader.Type == ArchiveType.Tar);
    }

    [Fact]
    public async ValueTask Tar_With_TarGz_With_Flushed_EntryStream_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsTarGz.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);
        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.Equal("inner.tar.gz", reader.Entry.Key);

#if !LEGACY_DOTNET
        await using var entryStream = await reader.OpenEntryStreamAsync();
        await using var flushingStream = new FlushOnDisposeStream(entryStream);
#else
        using var entryStream = await reader.OpenEntryStreamAsync();
        using var flushingStream = new FlushOnDisposeStream(entryStream);
#endif

        // Extract inner.tar.gz
        await using var innerReader = await ReaderFactory.OpenAsyncReader(flushingStream);
        Assert.True(await innerReader.MoveToNextEntryAsync());
        Assert.Equal("test", innerReader.Entry.Key);
    }

    [Fact]
    public async ValueTask Tar_Broken_Stream_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));
        var memoryStream = new MemoryStream();

        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.True(await reader.MoveToNextEntryAsync());
        await reader.WriteEntryToAsync(memoryStream);
        stream.Close();
        await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
            await reader.MoveToNextEntryAsync()
        );
    }

    [Fact]
    public async ValueTask Tar_Corrupted_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "TarCorrupted.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        await using var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(stream));
        var memoryStream = new MemoryStream();

        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.True(await reader.MoveToNextEntryAsync());
        await reader.WriteEntryToAsync(memoryStream);
        stream.Close();
        await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
            await reader.MoveToNextEntryAsync()
        );
    }
}
