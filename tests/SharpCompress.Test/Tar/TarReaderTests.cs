using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Factories;
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
        using var reader = ReaderFactory.OpenReader(stream);
        var x = 0;
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                x++;
                if (x % 2 == 0)
                {
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH);
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
    public void Tar_BZip2_Reader_NonSeekable()
    {
        // Regression test for: Dynamic default RingBuffer for BZip2
        // Opening a .tar.bz2 from a non-seekable stream should succeed
        // because the ring buffer is sized to hold the BZip2 block before calling IsTarFile.
        using var fs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2"));
        using var nonSeekable = new ForwardOnlyStream(fs);
        using var reader = ReaderFactory.OpenReader(nonSeekable);
        var entryCount = 0;
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                entryCount++;
            }
        }
        Assert.True(entryCount > 0);
    }

    [Fact]
    public void TarWrapper_BZip2_MinimumRewindBufferSize_IsMaxBZip2BlockSize()
    {
        // The BZip2 TarWrapper must declare a MinimumRewindBufferSize large enough
        // to hold an entire maximum-size compressed BZip2 block (9 × 100 000 bytes).
        var bzip2Wrapper = Array.Find(
            TarWrapper.Wrappers,
            w => w.CompressionType == CompressionType.BZip2
        );
        Assert.NotNull(bzip2Wrapper);
        Assert.Equal(BZip2Constants.baseBlockSize * 9, bzip2Wrapper.MinimumRewindBufferSize);
    }

    [Fact]
    public void TarWrapper_Default_MinimumRewindBufferSize_Is_DefaultRewindableBufferSize()
    {
        // Non-BZip2 wrappers that don't specify a custom size default to
        // Constants.RewindableBufferSize so existing behaviour is unchanged.
        var noneWrapper = Array.Find(
            TarWrapper.Wrappers,
            w => w.CompressionType == CompressionType.None
        );
        Assert.NotNull(noneWrapper);
        Assert.Equal(Common.Constants.RewindableBufferSize, noneWrapper.MinimumRewindBufferSize);
    }

    [Fact]
    public void Tar_BZip2_Entry_Stream()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2")))
        using (var reader = TarReader.OpenReader(stream))
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
    public void Tar_PaxLocalHeader_Reader()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxLocalHeader.tar");

        using Stream stream = File.OpenRead(archivePath);
        using var reader = TarReader.OpenReader(stream);

        Assert.True(reader.MoveToNextEntry());
        var firstEntry = (TarEntry)reader.Entry;
        Assert.Equal("pax/overridden-name.txt", firstEntry.Key);
        Assert.Equal(10, firstEntry.Size);
        Assert.Equal(1234, firstEntry.UserID);
        Assert.Equal(2345, firstEntry.GroupId);
        Assert.Equal(Convert.ToInt64("640", 8), firstEntry.Mode);

        var expectedTime = DateTimeOffset.FromUnixTimeSeconds(1700000000).LocalDateTime;
        Assert.Equal(expectedTime, firstEntry.LastModifiedTime);

        using (var entryStream = reader.OpenEntryStream())
        using (var memoryStream = new MemoryStream())
        {
            entryStream.CopyTo(memoryStream);
            Assert.Equal(10, memoryStream.Length);
        }

        Assert.True(reader.MoveToNextEntry());
        var secondEntry = (TarEntry)reader.Entry;
        Assert.Equal("second.txt", secondEntry.Key);
        Assert.Equal(11, secondEntry.UserID);
        Assert.Equal(22, secondEntry.GroupId);
        Assert.Equal(Convert.ToInt64("644", 8), secondEntry.Mode);
        Assert.Equal(2, secondEntry.Size);

        Assert.False(reader.MoveToNextEntry());
    }

    [Fact]
    public void Tar_PaxLocalHeader_Link_Reader()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxLocalHeader.Link.tar");

        using Stream stream = File.OpenRead(archivePath);
        using var reader = TarReader.OpenReader(stream);

        Assert.True(reader.MoveToNextEntry());
        Assert.Equal("pax/link-entry", reader.Entry.Key);
        Assert.Equal("pax/target-entry", reader.Entry.LinkTarget);
        Assert.False(reader.Entry.IsDirectory);
        Assert.False(reader.MoveToNextEntry());
    }

    [Fact]
    public void Tar_PaxGlobalHeader_Reader()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxGlobalHeader.tar");

        using Stream stream = File.OpenRead(archivePath);
        using var reader = TarReader.OpenReader(stream);

        var globalTime = DateTimeOffset.FromUnixTimeSeconds(1700000100).LocalDateTime;
        var localOverrideTime = DateTimeOffset.FromUnixTimeSeconds(1700000200).LocalDateTime;

        Assert.True(reader.MoveToNextEntry());
        var firstEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-one.txt", firstEntry.Key);
        Assert.Equal(4000, firstEntry.UserID);
        Assert.Equal(5000, firstEntry.GroupId);
        Assert.Equal(Convert.ToInt64("640", 8), firstEntry.Mode);
        Assert.Equal(globalTime, firstEntry.LastModifiedTime);

        Assert.True(reader.MoveToNextEntry());
        var secondEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-local-override.txt", secondEntry.Key);
        Assert.Equal(4010, secondEntry.UserID);
        Assert.Equal(5010, secondEntry.GroupId);
        Assert.Equal(Convert.ToInt64("600", 8), secondEntry.Mode);
        Assert.Equal(localOverrideTime, secondEntry.LastModifiedTime);

        Assert.True(reader.MoveToNextEntry());
        var thirdEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-three.txt", thirdEntry.Key);
        Assert.Equal(4000, thirdEntry.UserID);
        Assert.Equal(5000, thirdEntry.GroupId);
        Assert.Equal(Convert.ToInt64("640", 8), thirdEntry.Mode);
        Assert.Equal(globalTime, thirdEntry.LastModifiedTime);

        Assert.False(reader.MoveToNextEntry());
    }

    [Fact]
    public void Tar_PaxGlobalHeader_Link_Reader()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.PaxGlobalHeader.Link.tar");

        using Stream stream = File.OpenRead(archivePath);
        using var reader = TarReader.OpenReader(stream);

        Assert.True(reader.MoveToNextEntry());
        var firstEntry = (TarEntry)reader.Entry;
        Assert.Equal("global-link", firstEntry.Key);
        Assert.Equal("global-target", firstEntry.LinkTarget);
        Assert.Equal(4100, firstEntry.UserID);
        Assert.Equal(5100, firstEntry.GroupId);
        Assert.Equal(Convert.ToInt64("777", 8), firstEntry.Mode);

        Assert.True(reader.MoveToNextEntry());
        var secondEntry = (TarEntry)reader.Entry;
        Assert.Equal("local-link-override", secondEntry.Key);
        Assert.Equal("local-target", secondEntry.LinkTarget);
        Assert.Equal(4100, secondEntry.UserID);
        Assert.Equal(5100, secondEntry.GroupId);
        Assert.Equal(Convert.ToInt64("777", 8), secondEntry.Mode);

        Assert.False(reader.MoveToNextEntry());
    }

    [Fact]
    public void Tar_WithSymlink_Reader_SurfacesLinkTargets()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "TarWithSymlink.tar.gz");

        using Stream stream = File.OpenRead(archivePath);
        using var reader = TarReader.OpenReader(stream);

        var foundVulkanToolsLink = false;
        var foundVulkanSamplesLink = false;

        while (reader.MoveToNextEntry())
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
    public void Tar_BZip2_Skip_Entry_Stream()
    {
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2"));
        using var reader = TarReader.OpenReader(stream);
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
        using var reader = ReaderFactory.OpenReader(stream);
        Assert.True(reader.Type == ArchiveType.Tar);
    }

    [Fact]
    public void Tar_With_TarGz_With_Flushed_EntryStream()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsTarGz.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.OpenReader(stream);
        Assert.True(reader.MoveToNextEntry());
        Assert.Equal("inner.tar.gz", reader.Entry.Key);

        using var entryStream = reader.OpenEntryStream();
        using var flushingStream = new FlushOnDisposeStream(entryStream);

        // Extract inner.tar.gz
        using var innerReader = ReaderFactory.OpenReader(flushingStream);
        Assert.True(innerReader.MoveToNextEntry());
        Assert.Equal("test", innerReader.Entry.Key);
    }

    [Fact]
    public void Tar_Broken_Stream()
    {
        var archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        using Stream stream = File.OpenRead(archiveFullPath);
        using var reader = ReaderFactory.OpenReader(stream);
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
        using var reader = ReaderFactory.OpenReader(stream);
        var memoryStream = new MemoryStream();

        Assert.True(reader.MoveToNextEntry());
        Assert.True(reader.MoveToNextEntry());
        reader.WriteEntryTo(memoryStream);
        stream.Close();
        Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
    }

    [Fact]
    public void Tar_Malformed_LongName_Excessive_Size()
    {
        // Create a malformed TAR header with an excessively large LongName size
        // This simulates what happens during auto-detection of compressed files
        var buffer = new byte[512];

        // Set up a basic TAR header structure
        // Name field (offset 0, 100 bytes) - set to "././@LongLink" which is typical for LongName
        var nameBytes = System.Text.Encoding.ASCII.GetBytes("././@LongLink");
        Array.Copy(nameBytes, 0, buffer, 0, nameBytes.Length);

        // Set entry type to LongName (offset 156)
        buffer[156] = (byte)'L'; // EntryType.LongName

        // Set an excessively large size (offset 124, 12 bytes, octal format)
        // This simulates a corrupted/misinterpreted size field
        // Using "77777777777" (octal) = 8589934591 bytes (~8GB)
        var sizeBytes = System.Text.Encoding.ASCII.GetBytes("77777777777 ");
        Array.Copy(sizeBytes, 0, buffer, 124, sizeBytes.Length);

        // Calculate and set checksum (offset 148, 8 bytes)
        // Set checksum field to spaces first
        for (var i = 148; i < 156; i++)
        {
            buffer[i] = (byte)' ';
        }

        // Calculate checksum
        var checksum = 0;
        foreach (var b in buffer)
        {
            checksum += b;
        }

        var checksumStr = Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ";
        var checksumBytes = System.Text.Encoding.ASCII.GetBytes(checksumStr);
        Array.Copy(checksumBytes, 0, buffer, 148, checksumBytes.Length);

        // Create a stream with this malformed header
        using var stream = new MemoryStream();
        stream.Write(buffer, 0, buffer.Length);
        stream.Position = 0;

        // Attempt to read this malformed archive
        // The InvalidFormatException from the validation gets caught and converted to IncompleteArchiveException
        // The important thing is it doesn't cause OutOfMemoryException
        Assert.Throws<IncompleteArchiveException>(() =>
        {
            using var reader = TarReader.OpenReader(stream);
            reader.MoveToNextEntry();
        });
    }
}
