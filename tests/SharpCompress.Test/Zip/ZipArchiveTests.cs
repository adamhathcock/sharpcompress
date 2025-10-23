using System;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipArchiveTests : ArchiveTests
{
    public ZipArchiveTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void Zip_ZipX_ArchiveStreamRead() => ArchiveStreamRead("Zip.zipx");

    [Fact]
    public void Zip_BZip2_Streamed_ArchiveStreamRead() => ArchiveStreamRead("Zip.bzip2.dd.zip");

    [Fact]
    public void Zip_BZip2_ArchiveStreamRead() => ArchiveStreamRead("Zip.bzip2.zip");

    [Fact]
    public void Zip_Deflate_Streamed2_ArchiveStreamRead() =>
        ArchiveStreamRead("Zip.deflate.dd-.zip");

    [Fact]
    public void Zip_Deflate_Streamed_ArchiveStreamRead() => ArchiveStreamRead("Zip.deflate.dd.zip");

    [Fact]
    public void Zip_Deflate_ArchiveStreamRead() => ArchiveStreamRead("Zip.deflate.zip");

    [Fact]
    public void Zip_Deflate64_ArchiveStreamRead() => ArchiveStreamRead("Zip.deflate64.zip");

    [Fact]
    public void Zip_LZMA_Streamed_ArchiveStreamRead() => ArchiveStreamRead("Zip.lzma.dd.zip");

    [Fact]
    public void Zip_LZMA_ArchiveStreamRead() => ArchiveStreamRead("Zip.lzma.zip");

    [Fact]
    public void Zip_PPMd_Streamed_ArchiveStreamRead() => ArchiveStreamRead("Zip.ppmd.dd.zip");

    [Fact]
    public void Zip_PPMd_ArchiveStreamRead() => ArchiveStreamRead("Zip.ppmd.zip");

    [Fact]
    public void Zip_None_ArchiveStreamRead() => ArchiveStreamRead("Zip.none.zip");

    [Fact]
    public void Zip_BZip2_Streamed_ArchiveFileRead() => ArchiveFileRead("Zip.bzip2.dd.zip");

    [Fact]
    public void Zip_BZip2_ArchiveFileRead() => ArchiveFileRead("Zip.bzip2.zip");

    [Fact]
    public void WinZip26_ArchiveFileRead() => ArchiveFileRead("WinZip26.zip");

    [Fact]
    public void WinZip26_Multi_ArchiveFileRead() =>
        ArchiveStreamMultiRead(null, "WinZip26.nocomp.multi.zip", "WinZip26.nocomp.multi.z01"); //min split size is 64k so no compression used

    [Fact]
    public void WinZip26_X_BZip2_ArchiveFileRead() => ArchiveFileRead("WinZip26_BZip2.zipx");

    [Fact]
    public void WinZip26_X_Lzma_ArchiveFileRead() => ArchiveFileRead("WinZip26_LZMA.zipx");

    [Fact]
    public void WinZip26_X_Multi_ArchiveFileRead() =>
        ArchiveStreamMultiRead(null, "WinZip26.nocomp.multi.zipx", "WinZip26.nocomp.multi.zx01"); //min split size is 64k so no compression used

    [Fact]
    public void WinZip27_X_XZ_ArchiveFileRead() => ArchiveFileRead("WinZip27_XZ.zipx");

    [Fact]
    public void Zip_Deflate_Streamed2_ArchiveFileRead() => ArchiveFileRead("Zip.deflate.dd-.zip");

    [Fact]
    public void Zip_Deflate_Streamed_ArchiveFileRead() => ArchiveFileRead("Zip.deflate.dd.zip");

    [Fact]
    public void Zip_Deflate_ArchiveFileRead() => ArchiveFileRead("Zip.deflate.zip");

    [Fact]
    public void Zip_Deflate_ArchiveExtractToDirectory() =>
        ArchiveExtractToDirectory("Zip.deflate.zip");

    //will detect and load other files
    [Fact]
    public void Zip_Deflate_Multi_ArchiveFirstFileRead() =>
        ArchiveFileRead("WinZip26.nocomp.multi.zip");

    //"WinZip26.nocomp.multi.z01"
    //will detect and load other files
    [Fact]
    public void ZipX_Deflate_Multi_ArchiveFirstFileRead() =>
        ArchiveFileRead("WinZip26.nocomp.multi.zipx");

    //"WinZip26.nocomp.multi.zx01"
    [Fact]
    public void Zip_GetParts() =>
        //uses first part to search for all parts and compares against this array
        ArchiveGetParts(new[] { "Infozip.nocomp.multi.zip", "Infozip.nocomp.multi.z01" });

    [Fact]
    public void ZipX_GetParts() =>
        //uses first part to search for all parts and compares against this array
        ArchiveGetParts(new[] { "WinZip26.nocomp.multi.zipx", "WinZip26.nocomp.multi.zx01" });

    [Fact]
    public void Zip_GetPartsSplit() =>
        //uses first part to search for all parts and compares against this array
        ArchiveGetParts(
            new[]
            {
                "Zip.deflate.split.001",
                "Zip.deflate.split.002",
                "Zip.deflate.split.003",
                "Zip.deflate.split.004",
                "Zip.deflate.split.005",
                "Zip.deflate.split.006",
            }
        );

    //will detect and load other files
    [Fact]
    public void Zip_Deflate_Split_ArchiveFirstFileRead() =>
        ArchiveFileRead("Zip.deflate.split.001");

    //"Zip.deflate.split.002",
    //"Zip.deflate.split.003",
    //"Zip.deflate.split.004",
    //"Zip.deflate.split.005",
    //"Zip.deflate.split.006"
    [Fact]
    public void Zip_Deflate_Split_ArchiveFileRead() =>
        ArchiveStreamMultiRead(
            null,
            "Zip.deflate.split.001",
            "Zip.deflate.split.002",
            "Zip.deflate.split.003",
            "Zip.deflate.split.004",
            "Zip.deflate.split.005",
            "Zip.deflate.split.006"
        );

    [Fact]
    public void Zip_InfoZip_Multi_ArchiveFileRead() =>
        ArchiveStreamMultiRead(null, "Infozip.nocomp.multi.zip", "Infozip.nocomp.multi.z01"); //min split size is 64k so no compression used

    [Fact]
    public void Zip_Deflate64_ArchiveFileRead() => ArchiveFileRead("Zip.deflate64.zip");

    [Fact]
    public void Zip_LZMA_Streamed_ArchiveFileRead() => ArchiveFileRead("Zip.lzma.dd.zip");

    [Fact]
    public void Zip_LZMA_ArchiveFileRead() => ArchiveFileRead("Zip.lzma.zip");

    [Fact]
    public void Zip_PPMd_Streamed_ArchiveFileRead() => ArchiveFileRead("Zip.ppmd.dd.zip");

    [Fact]
    public void Zip_PPMd_ArchiveFileRead() => ArchiveFileRead("Zip.ppmd.zip");

    [Fact]
    public void Zip_None_ArchiveFileRead() => ArchiveFileRead("Zip.none.zip");

    [Fact]
    public void Zip_Zip64_ArchiveStreamRead() => ArchiveStreamRead("Zip.zip64.zip");

    [Fact]
    public void Zip_Zip64_ArchiveFileRead() => ArchiveFileRead("Zip.zip64.zip");

    [Fact]
    public void Zip_Shrink_ArchiveStreamRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveStreamRead("Zip.shrink.zip");
    }

    [Fact]
    public void Zip_Implode_ArchiveStreamRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveStreamRead("Zip.implode.zip");
    }

    [Fact]
    public void Zip_Reduce1_ArchiveStreamRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveStreamRead("Zip.reduce1.zip");
    }

    [Fact]
    public void Zip_Reduce2_ArchiveStreamRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveStreamRead("Zip.reduce2.zip");
    }

    [Fact]
    public void Zip_Reduce3_ArchiveStreamRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveStreamRead("Zip.reduce3.zip");
    }

    [Fact]
    public void Zip_Reduce4_ArchiveStreamRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveStreamRead("Zip.reduce4.zip");
    }

    [Fact]
    public void Zip_Random_Write_Remove()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");

        using (var archive = ZipArchive.Open(unmodified))
        {
            var entry = archive.Entries.Single(x =>
                x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            );
            archive.RemoveEntry(entry);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            archive.SaveTo(scratchPath, writerOptions);
        }
        CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
    }

    [Fact]
    public void Zip_Random_Write_Add()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod2.zip");

        using (var archive = ZipArchive.Open(unmodified))
        {
            archive.AddEntry("jpg\\test.jpg", jpg);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            archive.SaveTo(scratchPath, writerOptions);
        }
        CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
    }

    [Fact]
    public void Zip_Save_Twice()
    {
        var scratchPath1 = Path.Combine(SCRATCH_FILES_PATH, "a.zip");
        var scratchPath2 = Path.Combine(SCRATCH_FILES_PATH, "b.zip");

        using (var arc = ZipArchive.Create())
        {
            var str = "test.txt";
            var source = new MemoryStream(Encoding.UTF8.GetBytes(str));
            arc.AddEntry("test.txt", source, true, source.Length);
            arc.SaveTo(scratchPath1, CompressionType.Deflate);
            arc.SaveTo(scratchPath2, CompressionType.Deflate);
        }

        Assert.Equal(new FileInfo(scratchPath1).Length, new FileInfo(scratchPath2).Length);
    }

    [Fact]
    public void Zip_Removal_Poly()
    {
        var scratchPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        using var vfs = (ZipArchive)ArchiveFactory.Open(scratchPath);
        var e = vfs.Entries.First(v =>
            v.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
        );
        vfs.RemoveEntry(e);
        Assert.Null(
            vfs.Entries.FirstOrDefault(v =>
                v.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            )
        );
        Assert.Null(
            ((IArchive)vfs).Entries.FirstOrDefault(v =>
                v.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    [Fact]
    public void Zip_Create_NoDups()
    {
        using var arc = ZipArchive.Create();
        arc.AddEntry("1.txt", new MemoryStream());
        Assert.Throws<ArchiveException>(() => arc.AddEntry("\\1.txt", new MemoryStream()));
    }

    [Fact]
    public void Zip_Create_Same_Stream()
    {
        var scratchPath1 = Path.Combine(SCRATCH_FILES_PATH, "a.zip");
        var scratchPath2 = Path.Combine(SCRATCH_FILES_PATH, "b.zip");

        using (var arc = ZipArchive.Create())
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("qwert")))
            {
                arc.AddEntry("1.txt", stream, false, stream.Length);
                arc.AddEntry("2.txt", stream, false, stream.Length);
                arc.SaveTo(scratchPath1, CompressionType.Deflate);
                arc.SaveTo(scratchPath2, CompressionType.Deflate);
            }
        }

        Assert.Equal(new FileInfo(scratchPath1).Length, new FileInfo(scratchPath2).Length);
    }

    [Fact]
    public void Zip_Create_New()
    {
        foreach (
            var file in Directory.EnumerateFiles(
                ORIGINAL_FILES_PATH,
                "*.*",
                SearchOption.AllDirectories
            )
        )
        {
            var newFileName = file.Substring(ORIGINAL_FILES_PATH.Length);
            if (
                newFileName.StartsWith(
                    Path.DirectorySeparatorChar.ToString(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                newFileName = newFileName.Substring(1);
            }
            newFileName = Path.Combine(SCRATCH_FILES_PATH, newFileName);
            var newDir = Path.GetDirectoryName(newFileName) ?? throw new ArgumentNullException();
            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(newDir);
            }
            File.Copy(file, newFileName);
        }
        var scratchPath = Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        using (var archive = ZipArchive.Create())
        {
            archive.AddAllFromDirectory(SCRATCH_FILES_PATH);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            archive.SaveTo(scratchPath, writerOptions);
        }
        CompareArchivesByPath(unmodified, scratchPath, Encoding.GetEncoding(866));
        Directory.Delete(SCRATCH_FILES_PATH, true);
    }

    /// <summary>
    /// Creates an empty zip file and attempts to read it right afterwards.
    /// Ensures that parsing file headers works even in that case
    /// </summary>
    [Fact]
    public void Zip_Create_Empty_And_Read()
    {
        var archive = ZipArchive.Create();

        var archiveStream = new MemoryStream();

        archive.SaveTo(archiveStream, CompressionType.LZMA);

        archiveStream.Position = 0;

        var readArchive = ArchiveFactory.Open(archiveStream);

        var count = readArchive.Entries.Count();

        Assert.Equal(0, count);
    }

    [Fact]
    public void Zip_Create_New_Add_Remove()
    {
        foreach (
            var file in Directory.EnumerateFiles(
                ORIGINAL_FILES_PATH,
                "*.*",
                SearchOption.AllDirectories
            )
        )
        {
            var newFileName = file.Substring(ORIGINAL_FILES_PATH.Length);
            if (
                newFileName.StartsWith(
                    Path.DirectorySeparatorChar.ToString(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                newFileName = newFileName.Substring(1);
            }
            newFileName = Path.Combine(SCRATCH_FILES_PATH, newFileName);
            var newDir = Path.GetDirectoryName(newFileName) ?? throw new ArgumentNullException();
            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(newDir);
            }
            File.Copy(file, newFileName);
        }
        var scratchPath = Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip");

        using (var archive = ZipArchive.Create())
        {
            archive.AddAllFromDirectory(SCRATCH_FILES_PATH);
            archive.RemoveEntry(
                archive.Entries.Single(x =>
                    x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
                )
            );
            Assert.Null(
                archive.Entries.FirstOrDefault(x =>
                    x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
                )
            );
        }
        Directory.Delete(SCRATCH_FILES_PATH, true);
    }

    [Fact]
    public void Zip_Deflate_WinzipAES_Read()
    {
        using (
            var reader = ZipArchive.Open(
                Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip"),
                new ReaderOptions { Password = "test" }
            )
        )
        {
            foreach (var entry in reader.Entries.Where(x => !x.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Zip_Deflate_WinzipAES_MultiOpenEntryStream()
    {
        using var reader = ZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES2.zip"),
            new ReaderOptions { Password = "test" }
        );
        foreach (var entry in reader.Entries.Where(x => !x.IsDirectory))
        {
            var stream = entry.OpenEntryStream();
            Assert.NotNull(stream);
            var ex = Record.Exception(() => stream = entry.OpenEntryStream());
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Zip_Read_Volume_Comment()
    {
        using var reader = ZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.zip64.zip"),
            new ReaderOptions { Password = "test" }
        );
        var isComplete = reader.IsComplete;
        Assert.Equal(1, reader.Volumes.Count);

        var expectedComment =
            "Encoding:utf-8 || Compression:Deflate levelDefault || Encrypt:None || ZIP64:Always\r\nCreated at 2017-Jan-23 14:10:43 || DotNetZip Tool v1.9.1.8\r\nTest zip64 archive";
        Assert.Equal(expectedComment, reader.Volumes.First().Comment);
    }

    [Fact]
    public void Zip_BZip2_Pkware_Read()
    {
        using (
            var reader = ZipArchive.Open(
                Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip"),
                new ReaderOptions { Password = "test" }
            )
        )
        {
            foreach (var entry in reader.Entries.Where(x => !x.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Zip_Random_Entry_Access()
    {
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        var a = ZipArchive.Open(unmodified);
        var count = 0;
        foreach (var e in a.Entries)
        {
            count++;
        }

        //Prints 3
        Assert.Equal(3, count);
        a.Dispose();

        a = ZipArchive.Open(unmodified);
        var count2 = 0;

        foreach (var e in a.Entries)
        {
            count2++;

            //Stop at last file
            if (count2 == count)
            {
                var s = e.OpenEntryStream();
                s.ReadByte(); //Actually access stream
                s.Dispose();
                break;
            }
        }

        var count3 = 0;
        foreach (var e in a.Entries)
        {
            count3++;
        }

        Assert.Equal(3, count3);
    }

    [Fact]
    public void Zip_Deflate_PKWear_Multipy_Entry_Access()
    {
        var zipFile = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.pkware.zip");

        using var fileStream = File.OpenRead(zipFile);
        using var archive = ArchiveFactory.Open(
            fileStream,
            new ReaderOptions { Password = "12345678" }
        );
        var entries = archive.Entries.Where(entry => !entry.IsDirectory);
        foreach (var entry in entries)
        {
            for (var i = 0; i < 100; i++)
            {
                using var memoryStream = new MemoryStream();
                using var entryStream = entry.OpenEntryStream();
                entryStream.CopyTo(memoryStream);
            }
        }
    }

#if WINDOWS
    [Fact]
    public void Zip_Evil_Throws_Exception_Windows()
    {
        var zipFile = Path.Combine(TEST_ARCHIVES_PATH, "Zip.Evil.zip");

        Assert.ThrowsAny<Exception>(() =>
        {
            using var archive = ZipArchive.Open(zipFile);
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        });
    }
#endif

    private class NonSeekableMemoryStream : MemoryStream
    {
        public override bool CanSeek => false;
    }

    [Fact]
    public void TestSharpCompressWithEmptyStream()
    {
        MemoryStream stream = new NonSeekableMemoryStream();

        using (var zipWriter = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
        {
            zipWriter.Write("foo.txt", new MemoryStream(Array.Empty<byte>()));
            zipWriter.Write("foo2.txt", new MemoryStream(new byte[10]));
        }

        stream = new MemoryStream(stream.ToArray());
        File.WriteAllBytes(Path.Combine(SCRATCH_FILES_PATH, "foo.zip"), stream.ToArray());

        using (var zipArchive = ZipArchive.Open(stream))
        {
            foreach (var entry in zipArchive.Entries)
            {
                using var entryStream = entry.OpenEntryStream();
                var tempStream = new MemoryStream();
                const int bufSize = 0x1000;
                var buf = new byte[bufSize];
                var bytesRead = 0;
                while ((bytesRead = entryStream.Read(buf, 0, bufSize)) > 0)
                {
                    tempStream.Write(buf, 0, bytesRead);
                }
            }
        }
    }

    [Fact]
    public void Zip_BadLocalExtra_Read()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.badlocalextra.zip");

        using var za = ZipArchive.Open(zipPath);
        var ex = Record.Exception(() =>
        {
            var firstEntry = za.Entries.First(x => x.Key == "first.txt");
            var buffer = new byte[4096];

            using var memoryStream = new MemoryStream();
            using var firstStream = firstEntry.OpenEntryStream();
            firstStream.CopyTo(memoryStream);
            Assert.Equal(199, memoryStream.Length);
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Zip_NoCompression_DataDescriptors_Read()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.datadescriptors.zip");

        using var za = ZipArchive.Open(zipPath);
        var firstEntry = za.Entries.First(x => x.Key == "first.txt");
        var buffer = new byte[4096];

        using (var memoryStream = new MemoryStream())
        using (var firstStream = firstEntry.OpenEntryStream())
        {
            firstStream.CopyTo(memoryStream);
            Assert.Equal(199, memoryStream.Length);
        }

        var len1 = 0;
        var buffer1 = new byte[firstEntry.Size + 256];

        using (var firstStream = firstEntry.OpenEntryStream())
        {
            len1 = firstStream.Read(buffer1, 0, buffer.Length);
        }

        Assert.Equal(199, len1);

#if !NETFRAMEWORK
        var len2 = 0;
        var buffer2 = new byte[firstEntry.Size + 256];

        using (var firstStream = firstEntry.OpenEntryStream())
        {
            len2 = firstStream.Read(buffer2.AsSpan());
        }
        Assert.Equal(len1, len2);
        Assert.Equal(buffer1, buffer2);
#endif
    }

    [Fact]
    public void Zip_LongComment_Read()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.LongComment.zip");

        using var za = ZipArchive.Open(zipPath);
        var count = za.Entries.Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void Zip_Zip64_CompressedSizeExtraOnly_Read()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.zip64.compressedonly.zip");

        using var za = ZipArchive.Open(zipPath);
        var firstEntry = za.Entries.First(x => x.Key == "test/test.txt");

        using var memoryStream = new MemoryStream();
        using var firstStream = firstEntry.OpenEntryStream();
        firstStream.CopyTo(memoryStream);
        Assert.Equal(15, memoryStream.Length);
    }

    [Fact]
    public void Zip_Uncompressed_Read_All()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.uncompressed.zip");
        using var stream = File.OpenRead(zipPath);
        var reader = ReaderFactory.Open(stream);
        var entries = 0;
        while (reader.MoveToNextEntry())
        {
            using (var entryStream = reader.OpenEntryStream())
            using (var target = new MemoryStream())
            {
                entryStream.CopyTo(target);
            }

            entries++;
        }
        Assert.Equal(4, entries);
    }

    [Fact]
    public void Zip_Uncompressed_Skip_All()
    {
        var keys = new[]
        {
            "Folder/File1.txt",
            "Folder/File2.rtf",
            "Folder2/File1.txt",
            "Folder2/File2.txt",
            "DEADBEEF",
        };
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.uncompressed.zip");
        using var stream = File.OpenRead(zipPath);
        var reader = ReaderFactory.Open(stream);
        var x = 0;
        while (reader.MoveToNextEntry())
        {
            Assert.Equal(keys[x], reader.Entry.Key);
            x++;
        }

        Assert.Equal(4, x);
    }

    [Fact]
    public void Zip_Forced_Ignores_UnicodePathExtra()
    {
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.UnicodePathExtra.zip");
        using (var stream = File.OpenRead(zipPath))
        {
            var reader = ReaderFactory.Open(
                stream,
                new ReaderOptions
                {
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Default = Encoding.GetEncoding("shift_jis"),
                    },
                }
            );
            reader.MoveToNextEntry();
            Assert.Equal("궖귛궖귙귪궖귗귪궖귙_wav.frq", reader.Entry.Key);
        }
        using (var stream = File.OpenRead(zipPath))
        {
            var reader = ReaderFactory.Open(
                stream,
                new ReaderOptions
                {
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Forced = Encoding.GetEncoding("shift_jis"),
                    },
                }
            );
            reader.MoveToNextEntry();
            Assert.Equal("きょきゅんきゃんきゅ_wav.frq", reader.Entry.Key);
        }
    }

    [Fact]
    public void TestDataDescriptorRead()
    {
        using var archive = ArchiveFactory.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.datadescriptors.zip")
        );
        var firstEntry = archive.Entries.First();
        Assert.Equal(199, firstEntry.Size);
        using var _ = firstEntry.OpenEntryStream();
        Assert.Equal(199, firstEntry.Size);
    }

    [Fact]
    public void Zip_EntryCommentAfterEntryRead()
    {
        using var archive = ZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.EntryComment.zip")
        );
        var firstEntry = archive.Entries.First();
        Assert.Equal(29, firstEntry.Comment!.Length);
        using var _ = firstEntry.OpenEntryStream();
        Assert.Equal(29, firstEntry.Comment.Length);
    }

    [Fact]
    public void Zip_FilePermissions()
    {
        using var archive = ArchiveFactory.Open(Path.Combine(TEST_ARCHIVES_PATH, "Zip.644.zip"));

        var firstEntry = archive.Entries.First();
        const int S_IFREG = 0x8000;
        const int expected = (S_IFREG | 0b110_100_100) << 16; // 0644 mode regular file
        Assert.Equal(expected, firstEntry.Attrib);
    }
}
