using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarReaderAsyncTests : ReaderTests
{
    public TarReaderAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task Tar_Reader_Async() => await ReadAsync("Tar.tar", CompressionType.None);

    [Fact]
    public async Task Tar_Skip_Async()
    {
        using Stream stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"))
        );
        using var reader = ReaderFactory.Open(stream);
        var x = 0;
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                x++;
                if (x % 2 == 0)
                {
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
    }

    [Fact]
    public async Task Tar_Z_Reader_Async() => await ReadAsync("Tar.tar.Z", CompressionType.Lzw);

    [Fact]
    public async Task Tar_BZip2_Reader_Async() =>
        await ReadAsync("Tar.tar.bz2", CompressionType.BZip2);

    [Fact]
    public async Task Tar_GZip_Reader_Async() =>
        await ReadAsync("Tar.tar.gz", CompressionType.GZip);

    [Fact]
    public async Task Tar_ZStandard_Reader_Async() =>
        await ReadAsync("Tar.tar.zst", CompressionType.ZStandard);

    [Fact]
    public async Task Tar_LZip_Reader_Async() =>
        await ReadAsync("Tar.tar.lz", CompressionType.LZip);

    [Fact]
    public async Task Tar_Xz_Reader_Async() => await ReadAsync("Tar.tar.xz", CompressionType.Xz);

    [Fact]
    public async Task Tar_GZip_OldGnu_Reader_Async() =>
        await ReadAsync("Tar.oldgnu.tar.gz", CompressionType.GZip);

    [Fact]
    public async Task Tar_BZip2_Entry_Stream_Async()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2")))
        using (var reader = TarReader.Open(stream))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                    using var entryStream = reader.OpenEntryStream();
                    var file = Path.GetFileName(reader.Entry.Key);
                    var folder =
                        Path.GetDirectoryName(reader.Entry.Key)
                        ?? throw new ArgumentNullException();
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
        using (var reader = TarReader.Open(stream))
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
    public void Tar_BZip2_Skip_Entry_Stream_Async()
    {
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2"));
        using var reader = TarReader.Open(stream);
        var names = new List<string>();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                using var entryStream = reader.OpenEntryStream();
                entryStream.SkipEntry();
                names.Add(reader.Entry.Key.NotNull());
            }
        }
        Assert.Equal(3, names.Count);
    }

    [Fact]
    public void Tar_Containing_Rar_Reader_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        Assert.True(reader.ArchiveType == ArchiveType.Tar);
    }

    [Fact]
    public void Tar_With_TarGz_With_Flushed_EntryStream_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsTarGz.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        Assert.True(reader.MoveToNextEntry());
        Assert.Equal("inner.tar.gz", reader.Entry.Key);

        using var entryStream = reader.OpenEntryStream();
        using var flushingStream = new FlushOnDisposeStream(entryStream);

        // Extract inner.tar.gz
        using var innerReader = ReaderFactory.Open(flushingStream);
        Assert.True(innerReader.MoveToNextEntry());
        Assert.Equal("test", innerReader.Entry.Key);
    }

    [Fact]
    public async Task Tar_Broken_Stream_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        var memoryStream = new MemoryStream();

        Assert.True(reader.MoveToNextEntry());
        Assert.True(reader.MoveToNextEntry());
        await reader.WriteEntryToAsync(memoryStream);
        stream.Close();
        Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
    }

    [Fact]
    public async Task Tar_Corrupted_Async()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "TarCorrupted.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        var memoryStream = new MemoryStream();

        Assert.True(reader.MoveToNextEntry());
        Assert.True(reader.MoveToNextEntry());
        await reader.WriteEntryToAsync(memoryStream);
        stream.Close();
        Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
    }

#if LINUX
    [Fact]
    public async Task Tar_GZip_With_Symlink_Entries_Async()
    {
        using Stream stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "TarWithSymlink.tar.gz")
        );
        using var reader = TarReader.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }
            await reader.WriteEntryToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                    WriteSymbolicLink = (sourcePath, targetPath) =>
                    {
                        var link = new Mono.Unix.UnixSymbolicLinkInfo(sourcePath);
                        if (File.Exists(sourcePath))
                        {
                            link.Delete(); // equivalent to ln -s -f
                        }
                        link.CreateSymbolicLinkTo(targetPath);
                    },
                }
            );
            if (reader.Entry.LinkTarget != null)
            {
                var path = Path.Combine(SCRATCH_FILES_PATH, reader.Entry.Key.NotNull());
                var link = new Mono.Unix.UnixSymbolicLinkInfo(path);
                if (link.HasContents)
                {
                    // need to convert the link to an absolute path for comparison
                    var target = reader.Entry.LinkTarget;
                    var realTarget = Path.GetFullPath(
                        Path.Combine($"{Path.GetDirectoryName(path)}", target)
                    );

                    Assert.Equal(realTarget, link.GetContents().ToString());
                }
                else
                {
                    Assert.True(false, "Symlink has no target");
                }
            }
        }
    }
#endif
}
