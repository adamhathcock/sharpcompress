using System.IO;
using System.Threading.Tasks;
using SharpCompress.Compressors.ADC;
using Xunit;

namespace SharpCompress.Test;

public class AdcAsyncTest : TestBase
{
    [Fact]
    public async Task TestAdcStreamAsyncWholeChunk()
    {
        using var decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_decompressed.bin"));
        var decompressed = new byte[decFs.Length];
        decFs.Read(decompressed, 0, decompressed.Length);

        using var cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin"));
        using var decStream = new ADCStream(cmpFs);
        var test = new byte[262144];

        await decStream.ReadAsync(test, 0, test.Length);

        Assert.Equal(decompressed, test);
    }

    [Fact]
    public async Task TestAdcStreamAsync()
    {
        using var decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_decompressed.bin"));
        var decompressed = new byte[decFs.Length];
        decFs.Read(decompressed, 0, decompressed.Length);

        using var cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin"));
        using var decStream = new ADCStream(cmpFs);
        using var decMs = new MemoryStream();
        var test = new byte[512];
        var count = 0;

        do
        {
            count = await decStream.ReadAsync(test, 0, test.Length);
            decMs.Write(test, 0, count);
        } while (count > 0);

        Assert.Equal(decompressed, decMs.ToArray());
    }

    [Fact]
    public async Task TestAdcStreamAsyncWithCancellation()
    {
        using var cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin"));
        using var decStream = new ADCStream(cmpFs);
        var test = new byte[512];
        using var cts = new System.Threading.CancellationTokenSource();

        // Read should complete without cancellation
        var bytesRead = await decStream.ReadAsync(test, 0, test.Length, cts.Token);

        Assert.True(bytesRead > 0);
    }
}
