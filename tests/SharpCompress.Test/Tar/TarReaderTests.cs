using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarReaderTests : ReaderTests
{
    public TarReaderTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void Tar_Reader() => Read("Tar.tar", CompressionType.None);

    [Fact]
    public void Tar_Skip()
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
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
    }

    [Fact]
    public void Tar_Z_Reader() => Read("Tar.tar.Z", CompressionType.Lzw);

    [Fact]
    public void Tar_BZip2_Reader() => Read("Tar.tar.bz2", CompressionType.BZip2);

    [Fact]
    public void Tar_GZip_Reader() => Read("Tar.tar.gz", CompressionType.GZip);

    [Fact]
    public void Tar_ZStandard_Reader() => Read("Tar.tar.zst", CompressionType.ZStandard);

    [Fact]
    public void Tar_LZip_Reader() => Read("Tar.tar.lz", CompressionType.LZip);

    [Fact]
    public void Tar_Xz_Reader() => Read("Tar.tar.xz", CompressionType.Xz);

    [Fact]
    public void Tar_GZip_OldGnu_Reader() => Read("Tar.oldgnu.tar.gz", CompressionType.GZip);

    [Fact]
    public void Tar_BZip2_Entry_Stream()
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
                    entryStream.CopyTo(fs);
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Tar_LongNamesWithLongNameExtension()
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
    public void Tar_BZip2_Skip_Entry_Stream()
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
    public void Tar_Containing_Rar_Reader()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        Assert.True(reader.ArchiveType == ArchiveType.Tar);
    }

    [Fact]
    public void Tar_With_TarGz_With_Flushed_EntryStream()
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
    public void Tar_Broken_Stream()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        var memoryStream = new MemoryStream();

        Assert.True(reader.MoveToNextEntry());
        Assert.True(reader.MoveToNextEntry());
        reader.WriteEntryTo(memoryStream);
        stream.Close();
        Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
    }

    [Fact]
    public void Tar_Corrupted()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "TarCorrupted.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.Open(stream);
        var memoryStream = new MemoryStream();

        Assert.True(reader.MoveToNextEntry());
        Assert.True(reader.MoveToNextEntry());
        reader.WriteEntryTo(memoryStream);
        stream.Close();
        Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
    }

#if !NETFRAMEWORK
    [Fact]
    public void Tar_GZip_With_Symlink_Entries()
    {
        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows
        );
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
            reader.WriteEntryToDirectory(
                SCRATCH_FILES_PATH,
                new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                    WriteSymbolicLink = (sourcePath, targetPath) =>
                    {
                        if (!isWindows)
                        {
                            var link = new Mono.Unix.UnixSymbolicLinkInfo(sourcePath);
                            if (File.Exists(sourcePath))
                            {
                                link.Delete(); // equivalent to ln -s -f
                            }
                            link.CreateSymbolicLinkTo(targetPath);
                        }
                    },
                }
            );
            if (!isWindows)
            {
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
    }
#endif
}
