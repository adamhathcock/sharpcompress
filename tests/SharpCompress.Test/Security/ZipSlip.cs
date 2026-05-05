#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Archives;
using SharpCompress.Common;
using Xunit;
using SysZip = System.IO.Compression.ZipArchive;
using SysZipMode = System.IO.Compression.ZipArchiveMode;

namespace SharpCompress.Test.Security;

public class ZipSlip: TestBase
{

    [Fact]
public void RunSync()
{
    Console.WriteLine("--- Sync: archive.WriteToDirectory() ---");
    var (extractDir, parentDir) = SetupDirs("sync");
    Directory.CreateDirectory(extractDir);
    var archivePath = Path.Combine(parentDir, "malicious.zip");

    BuildMaliciousZip(archivePath);

    using var archive = ArchiveFactory.OpenArchive(archivePath);

    var ex = Assert.Throws<ExtractionException>(() =>
        archive.WriteToDirectory(extractDir, new ExtractionOptions { ExtractFullPath = true }));
    ex.Message.Should().Contain("Entry is trying to create a directory outside of the destination directory");

    CheckResults(parentDir, extractDir);
}

    [Fact]
public async Task RunAsync()
{
    Console.WriteLine("--- Async: archive.WriteToDirectoryAsync() ---");
    var (extractDir, parentDir) = SetupDirs("async");
    Directory.CreateDirectory(extractDir);
    var archivePath = Path.Combine(parentDir, "malicious.zip");

    BuildMaliciousZip(archivePath);

    var archive = await ArchiveFactory.OpenAsyncArchive(archivePath);
    await using (archive)
    {

        var ex = await Assert.ThrowsAsync<ExtractionException>(async () =>

                                                        await archive.WriteToDirectoryAsync(extractDir,
                                                                                            new ExtractionOptions
                                                                                            {
                                                                                                ExtractFullPath = true
                                                                                            }));
            ex.Message.Should().Contain("Entry is trying to create a directory outside of the destination directory");
    }

    CheckResults(parentDir, extractDir);
}

// Craft a ZIP with malicious directory entries using System.IO.Compression
// so we bypass any SharpCompress write-side normalisation.
static void BuildMaliciousZip(string path)
{
    using var fs = File.Create(path);
    using var zip = new SysZip(fs, SysZipMode.Create);

    // 1. Relative traversal: two levels up, then "escaped_relative/"
    zip.CreateEntry("../../escaped_relative/");

    // 2. Absolute Unix path (Path.Combine discards the base when second arg is rooted)
    zip.CreateEntry("/tmp/escaped_absolute/");

    // 3. A legitimate entry for contrast
    zip.CreateEntry("safe_subdir/");
}

static (string extractDir, string parentDir) SetupDirs(string label)
{
    var parentDir = Path.Combine(TEST_ARCHIVES_PATH, $"sc_poc_{label}_{Path.GetRandomFileName()}");
    Directory.CreateDirectory(parentDir);
    var extractDir = Path.Combine(parentDir, "extract_target");

    Console.WriteLine($"  Parent  : {parentDir}");
    Console.WriteLine($"  Target  : {extractDir}");
    return (extractDir, parentDir);
}

static void CheckResults(string parentDir, string extractDir)
{
    Console.WriteLine("  Directories created after extraction:");
    foreach (var d in Directory.GetDirectories(parentDir, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(parentDir, d);
        var escaped = !d.StartsWith(extractDir, StringComparison.Ordinal);
        Console.WriteLine($"    {(escaped ? "[ESCAPED]" : "[  ok   ]")} {relative}");
    }

    // Relative traversal "../../escaped_relative/" escapes two levels above extractDir
    // (which is parentDir/extract_target), landing in Path.GetTempPath()
    var relTarget = Path.GetFullPath(Path.Combine(extractDir, "../../escaped_relative"));
    if (Directory.Exists(relTarget))
    {
        Console.WriteLine($"    [ESCAPED] relative traversal created: {relTarget}");
        Directory.Delete(relTarget);
    }

    var absTarget = "/tmp/escaped_absolute";
    if (Directory.Exists(absTarget))
    {
        Console.WriteLine($"    [ESCAPED] absolute path created: {absTarget}");
        Directory.Delete(absTarget);
    }
}
}
#endif
