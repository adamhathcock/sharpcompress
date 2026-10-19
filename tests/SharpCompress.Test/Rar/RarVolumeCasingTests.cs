using System;
using System.IO;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarVolumeCasingTests : TestBase
{
    [Theory]
    [InlineData("sample.RAR", "SAMPLE.R00")]
    [InlineData("sample.part01.RAR", "SAMPLE.PART02.rar")]
    [InlineData("sample.rar.001", "SAMPLE.RAR.002")]
    public void Companion_Lookup_Is_Case_Insensitive(string firstName, string nextName)
    {
        var first = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, firstName));
        var next = Path.Combine(SCRATCH_FILES_PATH, nextName);
        File.WriteAllBytes(next, []);
        Assert.Equal(
            next,
            RarArchiveVolumeFactory.GetFilePart(1, first)!.FullName,
            ignoreCase: true
        );
        Assert.Null(RarArchiveVolumeFactory.GetFilePart(2, first));
    }

    [Fact]
    public void Companion_Lookup_Prefers_Exact_Path_And_Rejects_Ambiguity()
    {
        var first = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "sample.rar"));
        var upper = Path.Combine(SCRATCH_FILES_PATH, "SAMPLE.R00");
        var mixed = Path.Combine(SCRATCH_FILES_PATH, "Sample.r00");
        File.WriteAllBytes(upper, []);
        if (File.Exists(mixed))
        {
            return; // Case-insensitive filesystems cannot represent ambiguous companion names.
        }
        File.WriteAllBytes(mixed, []);
        Assert.Throws<InvalidFormatException>(() => RarArchiveVolumeFactory.GetFilePart(1, first));
        var exact = Path.Combine(SCRATCH_FILES_PATH, "sample.r00");
        File.WriteAllBytes(exact, []);
        Assert.Equal(exact, RarArchiveVolumeFactory.GetFilePart(1, first)!.FullName);
    }
}
