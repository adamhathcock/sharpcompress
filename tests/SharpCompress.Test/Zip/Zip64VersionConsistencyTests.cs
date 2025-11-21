using System;
using System.Buffers.Binary;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Tests for verifying version consistency between Local File Header (LFH)
/// and Central Directory File Header (CDFH) when using Zip64.
/// </summary>
public class Zip64VersionConsistencyTests : WriterTests
{
    public Zip64VersionConsistencyTests()
        : base(ArchiveType.Zip) { }

    [Fact]
    public void Zip64_Small_File_With_UseZip64_Should_Have_Matching_Versions()
    {
        // Create a zip with UseZip64=true but with a small file
        var filename = Path.Combine(SCRATCH2_FILES_PATH, "zip64_version_test.zip");

        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        // Create archive with UseZip64=true
        WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate)
        {
            LeaveStreamOpen = false,
            UseZip64 = true,
        };

        ZipArchive zipArchive = ZipArchive.Create();
        zipArchive.AddEntry("empty", new MemoryStream());
        zipArchive.SaveTo(filename, writerOptions);

        // Now read the raw bytes to verify version consistency
        using var fs = File.OpenRead(filename);
        using var br = new BinaryReader(fs);

        // Read Local File Header
        var lfhSignature = br.ReadUInt32();
        Assert.Equal(0x04034b50u, lfhSignature); // Local file header signature

        var lfhVersion = br.ReadUInt16();

        // Skip to Central Directory
        // Find Central Directory by searching from the end
        fs.Seek(-22, SeekOrigin.End); // Min EOCD size
        var eocdSignature = br.ReadUInt32();

        if (eocdSignature != 0x06054b50u)
        {
            // Might have Zip64 EOCD, search backwards
            fs.Seek(-100, SeekOrigin.End);
            var buffer = new byte[100];
            fs.Read(buffer, 0, 100);

            // Find EOCD signature
            for (int i = buffer.Length - 4; i >= 0; i--)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(i)) == 0x06054b50u)
                {
                    fs.Seek(-100 + i, SeekOrigin.End);
                    break;
                }
            }
        }

        // Read EOCD
        fs.Seek(-22, SeekOrigin.End);
        br.ReadUInt32(); // EOCD signature
        br.ReadUInt16(); // disk number
        br.ReadUInt16(); // disk with central dir
        br.ReadUInt16(); // entries on this disk
        br.ReadUInt16(); // total entries
        br.ReadUInt32(); // central directory size (unused)
        var cdOffset = br.ReadUInt32();

        // If Zip64, need to read from Zip64 EOCD
        if (cdOffset == 0xFFFFFFFF)
        {
            // Find Zip64 EOCD Locator
            fs.Seek(-22 - 20, SeekOrigin.End);
            var z64eocdlSig = br.ReadUInt32();
            if (z64eocdlSig == 0x07064b50u)
            {
                br.ReadUInt32(); // disk number
                var z64eocdOffset = br.ReadUInt64();
                br.ReadUInt32(); // total disks

                // Read Zip64 EOCD
                fs.Seek((long)z64eocdOffset, SeekOrigin.Begin);
                br.ReadUInt32(); // signature
                br.ReadUInt64(); // size of EOCD64
                br.ReadUInt16(); // version made by
                br.ReadUInt16(); // version needed
                br.ReadUInt32(); // disk number
                br.ReadUInt32(); // disk with CD
                br.ReadUInt64(); // entries on disk
                br.ReadUInt64(); // total entries
                br.ReadUInt64(); // CD size
                cdOffset = (uint)br.ReadUInt64(); // CD offset
            }
        }

        // Read Central Directory Header
        fs.Seek(cdOffset, SeekOrigin.Begin);
        var cdhSignature = br.ReadUInt32();
        Assert.Equal(0x02014b50u, cdhSignature); // Central directory header signature

        br.ReadUInt16(); // version made by
        var cdhVersionNeeded = br.ReadUInt16();

        // The versions should match when UseZip64 is true
        Assert.Equal(lfhVersion, cdhVersionNeeded);
    }

    [Fact]
    public void Zip64_Small_File_Without_UseZip64_Should_Have_Version_20()
    {
        // Create a zip without UseZip64
        var filename = Path.Combine(SCRATCH2_FILES_PATH, "no_zip64_version_test.zip");

        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        // Create archive without UseZip64
        WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate)
        {
            LeaveStreamOpen = false,
            UseZip64 = false,
        };

        ZipArchive zipArchive = ZipArchive.Create();
        zipArchive.AddEntry("empty", new MemoryStream());
        zipArchive.SaveTo(filename, writerOptions);

        // Read the raw bytes
        using var fs = File.OpenRead(filename);
        using var br = new BinaryReader(fs);

        // Read Local File Header version
        var lfhSignature = br.ReadUInt32();
        Assert.Equal(0x04034b50u, lfhSignature);
        var lfhVersion = br.ReadUInt16();

        // Read Central Directory Header version
        fs.Seek(-22, SeekOrigin.End);
        br.ReadUInt32(); // EOCD signature
        br.ReadUInt16(); // disk number
        br.ReadUInt16(); // disk with central dir
        br.ReadUInt16(); // entries on this disk
        br.ReadUInt16(); // total entries
        br.ReadUInt32(); // CD size
        var cdOffset = br.ReadUInt32();

        fs.Seek(cdOffset, SeekOrigin.Begin);
        var cdhSignature = br.ReadUInt32();
        Assert.Equal(0x02014b50u, cdhSignature);
        br.ReadUInt16(); // version made by
        var cdhVersionNeeded = br.ReadUInt16();

        // Both should be version 20 (or less)
        Assert.True(lfhVersion <= 20);
        Assert.Equal(lfhVersion, cdhVersionNeeded);
    }

    [Fact]
    public void LZMA_Compression_Should_Use_Version_63()
    {
        // Create a zip with LZMA compression
        var filename = Path.Combine(SCRATCH2_FILES_PATH, "lzma_version_test.zip");

        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        WriterOptions writerOptions = new ZipWriterOptions(CompressionType.LZMA)
        {
            LeaveStreamOpen = false,
            UseZip64 = false,
        };

        ZipArchive zipArchive = ZipArchive.Create();
        var data = new byte[100];
        new Random(42).NextBytes(data);
        zipArchive.AddEntry("test.bin", new MemoryStream(data));
        zipArchive.SaveTo(filename, writerOptions);

        // Read the raw bytes
        using var fs = File.OpenRead(filename);
        using var br = new BinaryReader(fs);

        // Read Local File Header version
        var lfhSignature = br.ReadUInt32();
        Assert.Equal(0x04034b50u, lfhSignature);
        var lfhVersion = br.ReadUInt16();

        // Read Central Directory Header version
        fs.Seek(-22, SeekOrigin.End);
        br.ReadUInt32(); // EOCD signature
        br.ReadUInt16(); // disk number
        br.ReadUInt16(); // disk with central dir
        br.ReadUInt16(); // entries on this disk
        br.ReadUInt16(); // total entries
        br.ReadUInt32(); // CD size
        var cdOffset = br.ReadUInt32();

        fs.Seek(cdOffset, SeekOrigin.Begin);
        var cdhSignature = br.ReadUInt32();
        Assert.Equal(0x02014b50u, cdhSignature);
        br.ReadUInt16(); // version made by
        var cdhVersionNeeded = br.ReadUInt16();

        // Both should be version 63 for LZMA
        Assert.Equal(63, lfhVersion);
        Assert.Equal(lfhVersion, cdhVersionNeeded);
    }

    [Fact]
    public void PPMd_Compression_Should_Use_Version_63()
    {
        // Create a zip with PPMd compression
        var filename = Path.Combine(SCRATCH2_FILES_PATH, "ppmd_version_test.zip");

        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        WriterOptions writerOptions = new ZipWriterOptions(CompressionType.PPMd)
        {
            LeaveStreamOpen = false,
            UseZip64 = false,
        };

        ZipArchive zipArchive = ZipArchive.Create();
        var data = new byte[100];
        new Random(42).NextBytes(data);
        zipArchive.AddEntry("test.bin", new MemoryStream(data));
        zipArchive.SaveTo(filename, writerOptions);

        // Read the raw bytes
        using var fs = File.OpenRead(filename);
        using var br = new BinaryReader(fs);

        // Read Local File Header version
        var lfhSignature = br.ReadUInt32();
        Assert.Equal(0x04034b50u, lfhSignature);
        var lfhVersion = br.ReadUInt16();

        // Read Central Directory Header version
        fs.Seek(-22, SeekOrigin.End);
        br.ReadUInt32(); // EOCD signature
        br.ReadUInt16(); // disk number
        br.ReadUInt16(); // disk with central dir
        br.ReadUInt16(); // entries on this disk
        br.ReadUInt16(); // total entries
        br.ReadUInt32(); // CD size
        var cdOffset = br.ReadUInt32();

        fs.Seek(cdOffset, SeekOrigin.Begin);
        var cdhSignature = br.ReadUInt32();
        Assert.Equal(0x02014b50u, cdhSignature);
        br.ReadUInt16(); // version made by
        var cdhVersionNeeded = br.ReadUInt16();

        // Both should be version 63 for PPMd
        Assert.Equal(63, lfhVersion);
        Assert.Equal(lfhVersion, cdhVersionNeeded);
    }

    [Fact]
    public void Zip64_Multiple_Small_Files_With_UseZip64_Should_Have_Matching_Versions()
    {
        // Create a zip with UseZip64=true but with multiple small files
        var filename = Path.Combine(SCRATCH2_FILES_PATH, "zip64_version_multiple_test.zip");

        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate)
        {
            LeaveStreamOpen = false,
            UseZip64 = true,
        };

        ZipArchive zipArchive = ZipArchive.Create();
        for (int i = 0; i < 5; i++)
        {
            var data = new byte[100];
            new Random(i).NextBytes(data);
            zipArchive.AddEntry($"file{i}.bin", new MemoryStream(data));
        }
        zipArchive.SaveTo(filename, writerOptions);

        // Verify that all entries have matching versions
        using var fs = File.OpenRead(filename);
        using var br = new BinaryReader(fs);

        // Read all LFH versions
        var lfhVersions = new System.Collections.Generic.List<ushort>();
        while (true)
        {
            var sig = br.ReadUInt32();
            if (sig == 0x04034b50u) // LFH signature
            {
                var version = br.ReadUInt16();
                lfhVersions.Add(version);

                // Skip rest of LFH
                br.ReadUInt16(); // flags
                br.ReadUInt16(); // compression
                br.ReadUInt32(); // mod time
                br.ReadUInt32(); // crc
                br.ReadUInt32(); // compressed size
                br.ReadUInt32(); // uncompressed size
                var fnLen = br.ReadUInt16();
                var extraLen = br.ReadUInt16();
                fs.Seek(fnLen + extraLen, SeekOrigin.Current);

                // Skip compressed data by reading compressed size from extra field if zip64
                // For simplicity in this test, we'll just find the next signature
                var found = false;

                while (fs.Position < fs.Length - 4)
                {
                    var b = br.ReadByte();
                    if (b == 0x50)
                    {
                        var nextBytes = br.ReadBytes(3);
                        if (
                            (nextBytes[0] == 0x4b && nextBytes[1] == 0x03 && nextBytes[2] == 0x04)
                            || // LFH
                            (nextBytes[0] == 0x4b && nextBytes[1] == 0x01 && nextBytes[2] == 0x02)
                        ) // CDH
                        {
                            fs.Seek(-4, SeekOrigin.Current);
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    break;
                }
            }
            else if (sig == 0x02014b50u) // CDH signature
            {
                break; // Reached central directory
            }
            else
            {
                break; // Unknown signature
            }
        }

        // Find Central Directory
        fs.Seek(-22, SeekOrigin.End);
        br.ReadUInt32(); // EOCD signature
        br.ReadUInt16(); // disk number
        br.ReadUInt16(); // disk with central dir
        br.ReadUInt16(); // entries on this disk
        var totalEntries = br.ReadUInt16();
        br.ReadUInt32(); // CD size
        var cdOffset = br.ReadUInt32();

        // Check if we need Zip64 EOCD
        if (cdOffset == 0xFFFFFFFF)
        {
            fs.Seek(-22 - 20, SeekOrigin.End);
            var z64eocdlSig = br.ReadUInt32();
            if (z64eocdlSig == 0x07064b50u)
            {
                br.ReadUInt32(); // disk number
                var z64eocdOffset = br.ReadUInt64();
                fs.Seek((long)z64eocdOffset, SeekOrigin.Begin);
                br.ReadUInt32(); // signature
                br.ReadUInt64(); // size
                br.ReadUInt16(); // version made by
                br.ReadUInt16(); // version needed
                br.ReadUInt32(); // disk number
                br.ReadUInt32(); // disk with CD
                br.ReadUInt64(); // entries on disk
                totalEntries = (ushort)br.ReadUInt64(); // total entries
                br.ReadUInt64(); // CD size
                cdOffset = (uint)br.ReadUInt64(); // CD offset
            }
        }

        // Read CDH versions
        fs.Seek(cdOffset, SeekOrigin.Begin);
        var cdhVersions = new System.Collections.Generic.List<ushort>();
        for (int i = 0; i < totalEntries; i++)
        {
            var sig = br.ReadUInt32();
            Assert.Equal(0x02014b50u, sig);
            br.ReadUInt16(); // version made by
            var version = br.ReadUInt16();
            cdhVersions.Add(version);

            // Skip rest of CDH
            br.ReadUInt16(); // flags
            br.ReadUInt16(); // compression
            br.ReadUInt32(); // mod time
            br.ReadUInt32(); // crc
            br.ReadUInt32(); // compressed size
            br.ReadUInt32(); // uncompressed size
            var fnLen = br.ReadUInt16();
            var extraLen = br.ReadUInt16();
            var commentLen = br.ReadUInt16();
            br.ReadUInt16(); // disk number start
            br.ReadUInt16(); // internal attributes
            br.ReadUInt32(); // external attributes
            br.ReadUInt32(); // LFH offset
            fs.Seek(fnLen + extraLen + commentLen, SeekOrigin.Current);
        }

        // Verify all versions match
        Assert.Equal(lfhVersions.Count, cdhVersions.Count);
        for (int i = 0; i < lfhVersions.Count; i++)
        {
            Assert.Equal(lfhVersions[i], cdhVersions[i]);
        }
    }
}
